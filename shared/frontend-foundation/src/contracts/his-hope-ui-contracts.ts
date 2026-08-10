export type HisHopeFilterOperator = 'eq' | 'neq' | 'contains' | 'startsWith' | 'gt' | 'gte' | 'lt' | 'lte' | 'in';
export type HisHopeFilterValue = string | number | boolean | null | string[] | number[];
export interface HisHopeTableFilter { key: string; operator: HisHopeFilterOperator; value: HisHopeFilterValue; }
export type HisHopeFilterJoin = 'and' | 'or';
export interface HisHopeFilterGroup { join: HisHopeFilterJoin; filters: HisHopeTableFilter[]; }
export interface HisHopePageQuery {
  page: number;
  pageSize: number;
  cursor?: string;
  sort?: HisHopeSort;
  search?: string;
  filters?: Record<string, HisHopeFilterValue>;
  filterItems?: HisHopeTableFilter[];
}
export interface HisHopeSortTerm { key: string; direction: 'asc' | 'desc'; }
export type HisHopeSort = HisHopeSortTerm | HisHopeSortTerm[];
export interface HisHopePageResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
  nextCursor?: string;
  previousCursor?: string;
}
export interface HisHopeBulkAction<T = Record<string, unknown>> { id: string; label: string; icon?: string; permission?: string; tone?: 'neutral' | 'danger'; execute?: (rows: T[]) => void; disabled?: boolean; }
export interface HisHopeBulkActionRequest<T = Record<string, unknown>> { actionId: string; rowKeys: string[]; query: HisHopePageQuery; selection?: HisHopeTableSelectionState; }
export interface HisHopeBulkRowProgress { rowKey: string; status: 'queued' | 'running' | 'completed' | 'failed' | 'skipped'; errorCode?: string; }
export interface HisHopeBulkJob { jobId: string; resource: string; actionId: string; status: 'queued' | 'running' | 'completed' | 'failed' | 'cancelled'; processed: number; total: number; rowProgress?: HisHopeBulkRowProgress[]; errorCode?: string; correlationId?: string; expiresAt?: string; retryable?: boolean; }
export type HisHopeTableSelectionScope = 'page' | 'all' | 'explicit';
export interface HisHopeTableSelectionState { scope: HisHopeTableSelectionScope; selectedKeys: string[]; excludedKeys: string[]; query: HisHopePageQuery; }
export type HisHopeTableExportFormat = 'csv' | 'xlsx' | 'json';
export interface HisHopeTableExportRequest { format: HisHopeTableExportFormat; columns: string[]; rowKeys: string[]; query: HisHopePageQuery; async?: boolean; maskSensitive?: boolean; auditReason?: string; }
export interface HisHopeTableEditorOption { value: string | number; label: string; disabled?: boolean; }
export type HisHopeTableEditor = 'text' | 'number' | 'date' | 'select' | 'autocomplete';
export type HisHopeTableEditStatus = 'clean' | 'dirty' | 'saving' | 'error';
export interface HisHopeTableEditState<T = Record<string, unknown>> { rowKey: string; draft: T; dirty: boolean; status: HisHopeTableEditStatus; saving?: boolean; error?: string; }
export interface HisHopeTableEditSaveRequest<T = Record<string, unknown>> { rowKey: string; previous: T; current: T; concurrencyToken?: string; }
export interface HisHopeTableEditConflict<T = Record<string, unknown>> { rowKey: string; submitted: T; latest: T; latestConcurrencyToken?: string; message: string; mergeableFields?: string[]; }
export type HisHopeConflictResolution = 'latest' | 'mine' | 'merge';
export interface HisHopeTableImportRequest { format: 'csv' | 'xlsx'; file: File; columns?: string[]; }
export interface HisHopeTableAnalysisRequest { operation: 'pivot' | 'aggregate' | 'formula'; query: HisHopePageQuery; columns: string[]; groupBy?: string; formulaId?: string; detailLimit?: number; }
export interface HisHopeTranslationDictionary { [key: string]: string | HisHopeTranslationDictionary; }
export interface HisHopeTranslator { t(key: string, fallback?: string, params?: Record<string, string | number>): string; locale: string; }
export interface HisHopeAuditFeedback { action: string; resource: string; resourceId?: string; outcome: 'started' | 'success' | 'failure'; message?: string; }
