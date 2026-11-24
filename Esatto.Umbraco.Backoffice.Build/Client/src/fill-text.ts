// A translated sentence that has a link or a <code> element inside it: the text marks the place with {0}, {1}, … and the element
// passes what goes there. The text stays one sentence for the translator, and nothing is set as raw HTML.

const MARKER = /^\{(\d+)\}$/;

/**
 * `text` as pieces: the text between the markers as strings, each marker replaced by the part with that number. A marker without a
 * part stays as it is, so a mistake shows instead of leaving a gap. Lit renders the returned array in order.
 */
export function fillText<T>(text: string, parts: readonly T[]): (string | T)[] {
  return text.split(/(\{\d+\})/).flatMap((piece): (string | T)[] => {
    if (!piece) return [];
    const index = MARKER.exec(piece)?.[1];
    return index !== undefined && Number(index) < parts.length ? [parts[Number(index)]] : [piece];
  });
}
