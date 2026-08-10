import '@angular/compiler';
import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';

setupZoneTestEnv();

function createJestFn() {
  const fn = jest.fn();
  (fn as any).and = {
    returnValue: (v: any) => fn.mockReturnValue(v),
    returnValues: (...vals: any[]) => { vals.forEach((v: any) => fn.mockReturnValueOnce(v)); return fn; },
    callThrough: () => fn.mockImplementation((x: any) => x),
    callFake: (impl: any) => fn.mockImplementation(impl),
    throwError: (e: any) => fn.mockRejectedValue(e),
    resolve: (v: any) => fn.mockResolvedValue(v),
    reject: (e: any) => fn.mockRejectedValue(e),
  };
  return fn;
}

(globalThis as any).jasmine = {
  createSpyObj: (name: string, methodNames: string[] | undefined, properties?: Record<string, any>) => {
    const methods = Array.isArray(methodNames) ? methodNames : methodNames ? [methodNames] : [];
    const obj: Record<string, any> = {};
    for (const method of methods) {
      obj[method] = createJestFn();
    }
    if (properties) {
      for (const [key, value] of Object.entries(properties)) {
        obj[key] = value;
      }
    }
    return obj;
  },
  createSpy: (name?: string) => createJestFn(),
  any: (type: any) => expect.any(type),
  objectContaining: (obj: any) => expect.objectContaining(obj),
  stringMatching: (pattern: any) => expect.stringMatching(pattern),
};

Object.defineProperty(globalThis, 'performance', {
  writable: true,
  value: {
    getEntriesByType: () => [],
    mark: () => {},
    measure: () => {},
    now: () => Date.now(),
  },
});

expect.extend({
  toBeTrue(received: boolean) {
    return {
      pass: received === true,
      message: () => `expected ${received} to be true`,
    };
  },
  toBeFalse(received: boolean) {
    return {
      pass: received === false,
      message: () => `expected ${received} to be false`,
    };
  },
});
