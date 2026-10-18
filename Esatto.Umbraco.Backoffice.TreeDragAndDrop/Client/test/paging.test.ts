import { describe, it, expect, vi } from 'vitest';
import { collectAllIds } from '../src/adapters/paging';

describe('collectAllIds', () => {
  it('reads every page until total is reached', async () => {
    const ids = Array.from({ length: 5 }, (_, i) => `id${i}`);
    const page = vi.fn(async (skip: number, take: number) => ({
      total: ids.length,
      items: ids.slice(skip, skip + take).map((id) => ({ id })),
    }));

    expect(await collectAllIds(page, 2)).toEqual(ids);
    expect(page.mock.calls).toEqual([[0, 2], [2, 2], [4, 2]]);
  });

  it('stops on an empty page even if total says otherwise', async () => {
    const page = vi.fn(async () => ({ total: 10, items: [] as { id: string }[] }));
    expect(await collectAllIds(page, 3)).toEqual([]);
    expect(page).toHaveBeenCalledOnce();
  });
});
