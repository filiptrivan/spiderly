import { reflectComponentType, Type, ɵtransitiveScopesFor as transitiveScopesFor } from '@angular/core';

import * as publicApi from '../../public-api';
import { SpiderlyControlsModule } from './spiderly-controls.module';
import { SpiderlyTextboxComponent } from './spiderly-textbox/spiderly-textbox.component';

// The Karma builder bundles the specs with webpack. Its parser turns require.context off, but not this.
declare global {
  interface ImportMeta {
    webpackContext(
      request: string,
      options: { recursive: boolean; regExp: RegExp },
    ): { keys(): string[]; (id: string): Record<string, unknown> };
  }
}

interface ControlComponent {
  type: Type<unknown>;
  file: string;
}

/** Each component class exported by a `*.component.ts` file under controls/, with that file. */
function controlComponents(): ControlComponent[] {
  const context = import.meta.webpackContext('./', { recursive: true, regExp: /\.component\.ts$/ });
  const controls = new Map<Type<unknown>, string>();
  for (const file of context.keys()) {
    for (const value of Object.values(context(file))) {
      if (typeof value === 'function' && reflectComponentType(value as Type<unknown>) !== null)
        controls.set(value as Type<unknown>, file);
    }
  }
  return [...controls].map(([type, file]) => ({ type, file }));
}

/** Each of `controls` that `exported` does not hold, by class and file. */
function missingFrom(controls: ControlComponent[], exported: Set<unknown>): string[] {
  return controls.filter((control) => !exported.has(control.type)).map(({ type, file }) => `${type.name} (${file})`);
}

// A generated details page imports SpiderlyControlsModule, and an app imports from the public API.
// A control missing from either one is a control that no app can use.
describe('Every control under controls/', () => {
  const controls = controlComponents();
  // Angular's private API: the components a module exports, re-exports included.
  const moduleExports = transitiveScopesFor(SpiderlyControlsModule).exported.directives;
  const publicApiExports = new Set<unknown>(Object.values(publicApi));

  it('is found by the file search', () => {
    expect(controls.map((control) => control.type)).toContain(SpiderlyTextboxComponent);
  });

  it('is exported by SpiderlyControlsModule', () => {
    expect(missingFrom(controls, moduleExports))
      .withContext('Add each to the imports and the exports of SpiderlyControlsModule')
      .toEqual([]);
  });

  it('is exported by the public API', () => {
    expect(missingFrom(controls, publicApiExports))
      .withContext("Add an `export * from './lib/controls/…'` line for each to src/public-api.ts")
      .toEqual([]);
  });

  it('is reported when the module and the public API leave it out (negative control)', () => {
    class LeftOutComponent {}
    const leftOut = [{ type: LeftOutComponent, file: 'left-out.component.ts' }];

    expect(missingFrom(leftOut, moduleExports)).toEqual(['LeftOutComponent (left-out.component.ts)']);
    expect(missingFrom(leftOut, publicApiExports)).toEqual(['LeftOutComponent (left-out.component.ts)']);
  });
});
