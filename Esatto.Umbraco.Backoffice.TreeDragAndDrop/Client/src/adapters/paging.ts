// Read every page of a paged tree endpoint ({ total, items }) and return the ids
// in server order. Pure apart from the page callback, so it's unit-tested.

export interface IdPage {
  total: number;
  items: ReadonlyArray<{ id: string }>;
}

export async function collectAllIds(
  page: (skip: number, take: number) => Promise<IdPage>,
  pageSize = 500,
): Promise<string[]> {
  const ids: string[] = [];
  for (;;) {
    const { total, items } = await page(ids.length, pageSize);
    for (const item of items) ids.push(item.id);
    // An empty page ends it even if `total` disagrees (items deleted mid-read).
    if (items.length === 0 || ids.length >= total) return ids;
  }
}
