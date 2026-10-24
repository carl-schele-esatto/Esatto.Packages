// Swedish texts. Looked up as esattoNavigationOrder_<key>.

export default {
  esattoNavigationOrder: {
    moved: (name: string, position: number, total: number) => `${name} flyttad till plats ${position} av ${total}`,
    saving: 'Sparar ordningen…',
    saveFailed: 'Den nya ordningen kunde inte sparas, så den har återställts.',
  },
};
