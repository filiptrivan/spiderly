/** Parses a JSON entry from web storage; missing or corrupted entries read as null. */
export function readStoredJson(storage: Storage, key: string): any {
  try {
    return JSON.parse(storage.getItem(key) ?? 'null');
  } catch {
    return null;
  }
}

/** Writes a value to web storage as JSON; the counterpart of readStoredJson. */
export function writeStoredJson(storage: Storage, key: string, value: any): void {
  storage.setItem(key, JSON.stringify(value));
}
