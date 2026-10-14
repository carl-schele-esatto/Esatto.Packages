async function l(o, s = 500) {
  const t = [];
  for (; ; ) {
    const { total: c, items: n } = await o(t.length, s);
    for (const e of n) t.push(e.id);
    if (n.length === 0 || t.length >= c) return t;
  }
}
export {
  l as c
};
//# sourceMappingURL=core-By19G-8s.js.map
