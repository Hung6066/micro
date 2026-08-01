import {
  createHisHopeCursorQuery,
  parseHisHopeDataTableQuery,
  sameHisHopeDataTableQuery,
  serializeHisHopeDataTableQuery,
  toggleHisHopeDataTableSort,
} from './his-hope-data-table.component';
import { toHisHopeTableEditorValue } from './his-hope-table-editor.component';

declare const describe: (label: string, spec: () => void) => void;
declare const it: (label: string, spec: () => void) => void;
declare const expect: (actual: unknown) => { toEqual(expected: unknown): void; toBe(expected: unknown): void; toBeNull(): void };

describe('HisHopeDataTable enterprise behavior', () => {
  it('round-trips server query state through URL-safe values', () => {
    const query = {
      page: 2,
      pageSize: 50,
      cursor: 'next/opaque token',
      search: '  patient  ',
      sort: [
        { key: 'status', direction: 'asc' as const },
        { key: 'createdAt', direction: 'desc' as const },
      ],
      filters: { active: true, department: ['cardiology', 'lab'] },
    };

    expect(parseHisHopeDataTableQuery(serializeHisHopeDataTableQuery(query))).toEqual({
      ...query,
      search: 'patient',
    });
  });

  it('does not treat equivalent initial URL state as a new server query', () => {
    expect(sameHisHopeDataTableQuery(
      { page: 1, pageSize: 20 },
      { page: 1, pageSize: 20, search: '  ', cursor: undefined },
    )).toBe(true);
    expect(sameHisHopeDataTableQuery(
      { page: 1, pageSize: 20 },
      { page: 2, pageSize: 20 },
    )).toBe(false);
  });

  it('cycles one sort term and appends shift-style terms without losing priority', () => {
    const first = toggleHisHopeDataTableSort([], 'status', false);
    const multi = toggleHisHopeDataTableSort(first, 'createdAt', true);
    const descending = toggleHisHopeDataTableSort(multi, 'status', true);

    expect(descending).toEqual([
      { key: 'createdAt', direction: 'asc' },
      { key: 'status', direction: 'desc' },
    ]);
  });

  it('creates a cursor query that preserves filters and resets numbered paging', () => {
    expect(createHisHopeCursorQuery({ page: 4, pageSize: 20, search: 'x' }, 'cursor-2')).toEqual({
      page: 1,
      pageSize: 20,
      search: 'x',
      cursor: 'cursor-2',
    });
  });

  it('coerces editor values according to the typed editor kind', () => {
    expect(toHisHopeTableEditorValue('42', 'number')).toBe(42);
    expect(toHisHopeTableEditorValue('', 'number')).toBeNull();
    expect(toHisHopeTableEditorValue('2026-07-26', 'date')).toBe('2026-07-26');
  });
});
