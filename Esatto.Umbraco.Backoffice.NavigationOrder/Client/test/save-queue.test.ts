import { describe, expect, it, vi } from 'vitest';
import { SaveQueue } from '../src/drag/save-queue';
import type { NavigationOrderData } from '../src/core/types';

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const order = (name: string): NavigationOrderData => ({ sections: [name], tabs: [] });
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

function setup() {
  const saves: { order: NavigationOrderData; reply: ReturnType<typeof deferred<NavigationOrderData>> }[] = [];
  const save = vi.fn((sent: NavigationOrderData) => {
    const reply = deferred<NavigationOrderData>();
    saves.push({ order: sent, reply });
    return reply.promise;
  });
  const apply = vi.fn();
  const onError = vi.fn();
  return { queue: new SaveQueue(save, apply, onError), saves, save, apply, onError };
}

const [c0, n1, n2, n3, s1] = ['c0', 'n1', 'n2', 'n3', 's1'].map(order);

describe('SaveQueue', () => {
  it('applies a move at once and saves it', () => {
    const { queue, saves, apply } = setup();

    void queue.submit(n1, c0);

    expect(apply).toHaveBeenCalledWith(n1);
    expect(saves.map((s) => s.order)).toEqual([n1]);
    expect(queue.busy).toBe(true);
  });

  it('saves one at a time, and only the latest of the moves made meanwhile', async () => {
    const { queue, saves, apply } = setup();
    const done = queue.submit(n1, c0);
    void queue.submit(n2, n1);
    void queue.submit(n3, n2);

    expect(apply.mock.calls.map(([o]) => o)).toEqual([n1, n2, n3]);
    expect(saves.map((s) => s.order)).toEqual([n1]);

    saves[0].reply.resolve(s1);
    await flush();
    expect(saves.map((s) => s.order)).toEqual([n1, n3]);

    saves[1].reply.resolve(n3);
    await done;
    expect(queue.busy).toBe(false);
  });

  it('puts back the order from before the first unsaved move when a save fails, dropping pending moves', async () => {
    const { queue, saves, apply, onError } = setup();
    const done = queue.submit(n1, c0);
    void queue.submit(n2, n1);
    void queue.submit(n3, n2);

    saves[0].reply.reject(new Error('offline'));
    await done;

    expect(apply).toHaveBeenLastCalledWith(c0);
    expect(onError).toHaveBeenCalledTimes(1);
    expect(saves).toHaveLength(1);
    expect(queue.busy).toBe(false);
  });

  it("puts back the server's last saved order when a later save fails", async () => {
    const { queue, saves, apply, onError } = setup();
    const done = queue.submit(n1, c0);
    void queue.submit(n3, n1);

    saves[0].reply.resolve(s1);
    await flush();
    saves[1].reply.reject(new Error('500'));
    await done;

    expect(apply).toHaveBeenLastCalledWith(s1);
    expect(onError).toHaveBeenCalledTimes(1);
  });

  it('starts from the current order again once idle', async () => {
    const { queue, saves, apply } = setup();
    const first = queue.submit(n1, c0);
    saves[0].reply.resolve(n1);
    await first;

    const second = queue.submit(n2, s1);
    saves[1].reply.reject(new Error('offline'));
    await second;

    expect(apply).toHaveBeenLastCalledWith(s1);
  });
});
