/**
 * Hands out a ticket per request; only the newest ticket is current, so a slower, older response
 * (e.g. 12 months, after the editor already picked 30 days) is ignored instead of shown.
 */
export class Latest {
  #ticket = 0;

  next(): number {
    return ++this.#ticket;
  }

  isCurrent(ticket: number): boolean {
    return ticket === this.#ticket;
  }
}
