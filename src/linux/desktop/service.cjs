const { spawn } = require('node:child_process');
const { EventEmitter } = require('node:events');
const { createInterface } = require('node:readline');
const { randomUUID } = require('node:crypto');

// 就绪及响应由同一条管道接收。后台任务结果只交付给发起请求的调用方。
class Service extends EventEmitter {
  #child;
  #pending = new Map();
  #failure;

  get disconnected() { return Boolean(this.#failure); }

  constructor(binary) {
    super();
    this.#child = spawn(binary, ['--desktop-service'], { stdio: ['pipe', 'pipe', 'pipe'] });
    let lastDiagnostic = '';
    this.#child.stderr.setEncoding('utf8').on('data', chunk => {
      lastDiagnostic = (lastDiagnostic + chunk).trim().slice(-1000);
      process.stderr.write(chunk);
    });
    this.#child.stdin.on('error', error => this.#disconnect(error));
    this.closed = new Promise(resolve => {
      this.#child.once('error', error => this.#disconnect(error));
      this.#child.once('close', (code, signal) => {
        this.#disconnect(new Error(lastDiagnostic || `通信服务已退出（${signal ?? code}）`));
        resolve(code === 0);
      });
    });
    this.ready = new Promise((resolve, reject) => {
      const startup = setTimeout(() => {
        this.#disconnect(new Error('等待通信服务就绪超时'));
        this.#child.stdin.end();
      }, 45000);
      this.once('disconnect', error => {
        clearTimeout(startup);
        reject(error);
      });
      const lines = createInterface({ input: this.#child.stdout });
      lines.once('line', line => {
        try {
          const hello = JSON.parse(line);
          if (hello.event !== 'ready' || hello.version !== 2 || hello.udpPort !== 38888 ||
              !Number.isInteger(hello.tcpPort) || hello.tcpPort < 38889 || hello.tcpPort > 38938) {
            throw new Error('通信服务版本或监听端口无效');
          }
          clearTimeout(startup);
          lines.on('line', message => this.#receive(message));
          resolve(hello);
        } catch (error) {
          this.#disconnect(error);
          this.#child.stdin.end();
        }
      });
    });
    this.ready.catch(() => {});
  }

  async request(method, args = {}, id = randomUUID()) {
    await this.ready;
    if (this.#failure) throw this.#failure;
    return new Promise((resolve, reject) => {
      const timer = method === 'file' ? null : setTimeout(() => {
        this.#pending.delete(id);
        reject(new Error('通信服务响应超时，操作结果未确认'));
      }, 20000);
      this.#pending.set(id, { resolve, reject, timer });
      this.#child.stdin.write(JSON.stringify({ id, method, ...args }) + '\n', error => {
        if (error) this.#disconnect(error);
      });
    });
  }

  async stop() {
    // 关闭继承的输入端即通知 ELF 正常退出，退出后才销毁 Electron。
    this.#child.stdin.end();
    const timer = setTimeout(() => this.#child.kill('SIGKILL'), 5000);
    try {
      return await this.closed;
    } finally {
      clearTimeout(timer);
    }
  }

  #receive(line) {
    try {
      const frame = JSON.parse(line);
      if (frame.event) {
        this.emit('event', frame);
        return;
      }
      const pending = this.#pending.get(frame.id);
      if (!pending) return;
      this.#pending.delete(frame.id);
      clearTimeout(pending.timer);
      if (frame.error) pending.reject(new Error(frame.error));
      else pending.resolve(frame.result);
    } catch (error) {
      this.#disconnect(error);
      this.#child.stdin.end();
    }
  }

  #disconnect(error) {
    if (this.#failure) return;
    this.#failure = error;
    for (const { reject, timer } of this.#pending.values()) {
      clearTimeout(timer);
      reject(new Error('通信服务已断开，操作结果未确认'));
    }
    this.#pending.clear();
    this.emit('disconnect', error);
  }
}

module.exports = Service;
