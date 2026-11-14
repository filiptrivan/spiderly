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

interface ControlFile {
  type: Type<unknown>;
  file: string;
}

/** Each component class exported by a `*.component.ts` file under controls/, with that file. */
function controlComponents(): ControlFile[] {
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

const named = ({ type, file }: ControlFile): string => `${type.name} (${file})`;

// A generated details page imports SpiderlyControlsModule and an app imports from the public API,
// so a control missing from either one is a control no app can use.
describe('Every control under controls/', () => {
  const controls = controlComponents();

  it('is found by the file search', () => {
    expect(controls.map((control) => control.type)).toContain(SpiderlyTextboxComponent);
  });

  it('is exported by SpiderlyControlsModule', () => {
    const exported = transitiveScopesFor(SpiderlyControlsModule).exported.directives;

    expect(controls.filter((control) => !exported.has(control.type)).map(named))
      .withContext('Add each to the imports and the exports of SpiderlyControlsModule')
      .toEqual([]);
  });

  it('is exported by the public API', () => {
    const exported = new Set<unknown>(Object.values(publicApi));

    expect(controls.filter((control) => !exported.has(control.type)).map(named))
      .withContext("Add an `export * from './lib/controls/…'` line for each to src/public-api.ts")
      .toEqual([]);
  });
});
