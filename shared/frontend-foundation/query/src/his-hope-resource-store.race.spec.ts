import { Subject } from 'rxjs';
import { HisHopeResourceStore } from './his-hope-resource-store';

describe('HisHopeResourceStore request ordering', () => {
  it('keeps loading state for the latest request', () => {
    const requests = new Map<string, Subject<string>>();
    const store = new HisHopeResourceStore<string, string>((query) => {
      const request = new Subject<string>();
      requests.set(query, request);
      return request;
    }, 'first');

    store.load();
    store.setQuery('second');
    requests.get('first')?.complete();
    expect(store.loading()).toBeTrue();
    requests.get('second')?.next('done');
    requests.get('second')?.complete();
    expect(store.loading()).toBeFalse();
    expect(store.data()).toBe('done');
  });
});
