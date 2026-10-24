// English texts (the fallback for every other language). Looked up as esattoNavigationOrder_<key>.

export default {
  esattoNavigationOrder: {
    moved: (name: string, position: number, total: number) => `${name} moved to position ${position} of ${total}`,
    saving: 'Saving the order…',
    saveFailed: "Couldn't save the new order, so it has been put back.",
  },
};
