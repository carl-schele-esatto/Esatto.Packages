import { describe, expect, it } from 'vitest';
import { Latest } from './latest';

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => (resolve = r));
  return { promise, resolve };
}

describe('Latest', () => {
  it('drops a response that arrives after a newer request was made', async () => {
    const latest = new Latest();
    const slow = deferred<string>();
    const fast = deferred<string>();

    const first = latest.run(() => slow.promise);
    const second = latest.run(() => fast.promise);
    fast.resolve('new filter');
    slow.resolve('old filter');

    expect(await second).toBe('new filter');
    expect(await first).toBeUndefined();
  });

  it('returns the response when nothing newer was asked for', async () => {
    const latest = new Latest();
    expect(await latest.run(() => Promise.resolve(42))).toBe(42);
  });
});
