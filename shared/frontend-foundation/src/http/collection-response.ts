export type CollectionEnvelope<T> =
  | T[]
  | { items?: T[]; clients?: T[]; roles?: T[]; users?: T[]; consents?: T[] };

export function unwrapCollection<T>(
  response: CollectionEnvelope<T>,
  preferredKey: 'clients' | 'roles' | 'users' | 'consents',
): T[] {
  if (Array.isArray(response)) {
    return response;
  }

  return response[preferredKey] ?? response.items ?? [];
}
