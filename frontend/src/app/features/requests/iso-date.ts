/** yyyy-MM-dd (the API format) from a calendar date picked in the user's local time zone. */
export function formatIsoDate(date: Date | null): string | null {
  if (date === null) {
    return null;
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${String(date.getFullYear()).padStart(4, '0')}-${month}-${day}`;
}

/** Local-midnight Date for a yyyy-MM-dd string, so the picker shows the same calendar day. */
export function parseIsoDate(value: string | null): Date | null {
  const match = value === null ? null : /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match === null ? null : new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}
