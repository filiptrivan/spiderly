// When we call it, inside the console, it will show SpiderlyError: ...
export class SpiderlyError extends Error {
  constructor(message: string) {
    super(message);
  }
}
