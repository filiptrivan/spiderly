import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import {
  createFilterStore,
  dateFilter,
  numberFilter,
  textFilter,
} from './filter-store';

// The claim this whole design rests on: a filter is an entity of its own, so it can exist and
// reach the server with no column anywhere near it. `Order.CompanyName` is the case that forced
// it — printed on ~82% of company rows, matched by no search, and nobody wants it as a column.
describe('createFilterStore — a filter needs no column', () => {
  it('serializes a set filter into the Filter.filters payload under its own id', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    expect(filters.toFilterPayload()).toEqual({
      companyName: [{ matchMode: MatchModeCodes.Contains, value: 'Elektromont' }],
    });
  });

  // `contains ''` matches every row, so a blanked box would claim to narrow the grid while showing
  // everything — and would draw a chip for it. Same class as the filter icon that filled on the
  // first keystroke (spiderly-data-table CLAUDE.md -> "Active-filter header icon").
  it('drops a filter whose value has been blanked, rather than sending an empty constraint', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    filters.set('companyName', { operator: MatchModeCodes.Contains, value: '' });
    filters.commit('companyName');

    expect(filters.toFilterPayload()).toEqual({});
  });

  // `In` is legal on a number and illegal on a string: the generated paginator answers it with
  // InvalidMatchMode. Today the only guard is a human not declaring it — `paymentGatewayCode` in
  // PACMS carries a hand-written comment saying exactly this. An engine that assembles operators
  // itself has removed that human, so it has to refuse — in BOTH halves. The `@ts-expect-error`
  // below pins the compile-time refusal (it fails the build if the line stops erroring), and the
  // thrown error covers what dodges the type system: a custom control, or a constraint restored
  // from an older persisted state.
  it('refuses an operator its value type does not allow', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    expect(() =>
      filters.set('companyName', {
        // @ts-expect-error `In` is not an operator a text filter accepts. This comment IS the
        // compile-time pin: if the line ever stops erroring, the build fails here.
        operator: MatchModeCodes.In,
        value: 'Elektromont',
      }),
    ).toThrowError(/companyName/);
  });
});

// The bar shows APPLIED, never TYPED. This is the one mistake in this component that has already
// shipped: reading `table.filters` filled the header's filter icon on the first keystroke, so the
// grid claimed to be narrowed while it still showed everything (spiderly-data-table CLAUDE.md ->
// "Active-filter header icon"). A chip drawn off a draft would be the same lie, louder.
describe('createFilterStore — typed is not applied', () => {
  it('keeps a set-but-uncommitted filter out of applied() and out of the payload', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elek',
    });

    expect(filters.applied()).toEqual([]);
    expect(filters.toFilterPayload()).toEqual({});
  });

  // The chip's x. It has to clear the DRAFT as well as the committed constraint: leaving the draft
  // behind empties the chip while the control still shows the old text, and the next commit brings
  // the filter back with nobody having typed anything. The last assertion is that trap.
  it('reset clears the chip, the payload key and the draft behind them', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    filters.reset('companyName');

    expect(filters.applied()).toEqual([]);
    expect(filters.toFilterPayload()).toEqual({});

    filters.commit('companyName');
    expect(filters.applied()).toEqual([]);
  });

  // The shape the bar draws a chip from. Asserted whole rather than by length, because the label
  // is plumbed from the DEFINITION while the operator and value come from the constraint, and a
  // chip reading "undefined: Elektromont" is the failure this catches.
  it('publishes a committed constraint to applied(), labelled from its definition', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    expect(filters.applied()).toEqual([
      {
        id: 'companyName',
        label: 'Firma',
        kind: 'text',
        operator: MatchModeCodes.Contains,
        operatorPhraseKey: 'FilterChipContains',
        value: 'Elektromont',
      },
    ]);
  });

  // The other direction of the SAME table. `In` is what a multiselect emits, and it is legal here
  // for exactly the reason it was refused on the text filter above: ALLOWED_OPERATORS says so,
  // once. `In` is also the only multi-valued operator, so its value is a list where every other
  // operator on the same kind takes a scalar.
  it('accepts In on a number filter, over a list of ids', () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    filters.set('orderStatusId', { operator: MatchModeCodes.In, value: [2, 3] });
    filters.commit('orderStatusId');

    expect(filters.toFilterPayload()).toEqual({
      orderStatusId: [{ matchMode: MatchModeCodes.In, value: [2, 3] }],
    });
  });

  // Compile-time only: nothing at runtime tells a list from a scalar, so this pin is the entire
  // guard. `In` is the multi-valued operator; every other one on the same kind takes a scalar.
  it('refuses a list on a scalar operator', () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    // @ts-expect-error `Equals` takes a single id, not a list.
    filters.set('orderStatusId', { operator: MatchModeCodes.Equals, value: [2, 3] });

    expect(filters.toFilterPayload()).toEqual({});
  });

  // The value reaches the payload as a Date, not pre-stringified: the API layer JSON-serializes
  // the whole `Filter`, and a store that stringified first would produce a double-encoded value
  // the moment that layer changed format.
  it('carries a Date through to the payload untouched', () => {
    const filters = createFilterStore({
      createdAt: dateFilter({ label: 'Datum' }),
    });
    const from = new Date('2026-09-01T00:00:00.000Z');

    filters.set('createdAt', {
      operator: MatchModeCodes.GreaterThan,
      value: from,
    });
    filters.commit('createdAt');

    expect(filters.toFilterPayload()).toEqual({
      createdAt: [{ matchMode: MatchModeCodes.GreaterThan, value: from }],
    });
  });

  // A half-typed datepicker yields `new Date("...")` that is Invalid. `JSON.stringify` turns that
  // into `null`, so the paginator would receive `{ matchMode: "greaterThan", value: null }` — a
  // constraint the operator can see a chip for and that narrows by nothing sane. Blank, not null.
  it('treats an invalid Date as blank rather than serializing it to null', () => {
    const filters = createFilterStore({
      createdAt: dateFilter({ label: 'Datum' }),
    });

    filters.set('createdAt', {
      operator: MatchModeCodes.GreaterThan,
      value: new Date('not a date'),
    });
    filters.commit('createdAt');

    expect(filters.applied()).toEqual([]);
    expect(filters.toFilterPayload()).toEqual({});
  });

  // The bar's "Clear all". Same draft rule as `reset`, for every filter at once: leaving drafts
  // behind leaves the controls full while the bar reports nothing applied, and the next commit
  // resurrects filters nobody re-entered. The pre-clear assertion is also the only place two
  // filters are shown composing into one payload.
  it('clear empties every filter, drafts included', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');
    filters.set('orderStatusId', {
      operator: MatchModeCodes.In,
      value: [2, 3],
    });
    filters.commit('orderStatusId');

    expect(filters.toFilterPayload()).toEqual({
      companyName: [{ matchMode: MatchModeCodes.Contains, value: 'Elektromont' }],
      orderStatusId: [{ matchMode: MatchModeCodes.In, value: [2, 3] }],
    });

    filters.clear();

    expect(filters.applied()).toEqual([]);
    expect(filters.toFilterPayload()).toEqual({});

    filters.commit('companyName');
    filters.commit('orderStatusId');
    expect(filters.applied()).toEqual([]);
  });

});

// The table's requery effect refetches on every change of `applied()`'s identity, so a commit
// that changes nothing must not publish a new identity. Draft-object identity is not the test:
// every `set` builds a fresh draft, and the case this exists for is a re-committed IDENTICAL
// value — a paste over itself, a keystroke undone inside one debounce window, Apply pressed
// twice. The page-side guard for this (`lastCommittedTerm` in PACMS's order list) is exactly the
// hand-rolled copy of this rule the store exists to retire.
describe('createFilterStore — a no-op commit publishes nothing', () => {
  it('re-committing an identical scalar constraint keeps applied() at the same identity', () => {
    const filters = createFilterStore({
      mixedSearch: textFilter({ label: 'Pretraga' }),
    });

    filters.set('mixedSearch', {
      operator: MatchModeCodes.Contains,
      value: 'bosch',
    });
    filters.commit('mixedSearch');
    const before = filters.applied();

    filters.set('mixedSearch', {
      operator: MatchModeCodes.Contains,
      value: 'bosch',
    });
    filters.commit('mixedSearch');

    expect(filters.applied()).toBe(before);
  });

  // A multiselect hands back a FRESH array on every change, identical contents included — ticking
  // a box and unticking it again must not refetch.
  it('re-committing an In list with the same ids in a new array keeps the identity', () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    filters.set('orderStatusId', { operator: MatchModeCodes.In, value: [2, 3] });
    filters.commit('orderStatusId');
    const before = filters.applied();

    filters.set('orderStatusId', { operator: MatchModeCodes.In, value: [2, 3] });
    filters.commit('orderStatusId');

    expect(filters.applied()).toBe(before);
  });

  // Two Date instances at the same instant are the same constraint. Without this, reopening a
  // date chip and pressing Apply refetches the identical query.
  it('re-committing the same instant in a new Date keeps the identity', () => {
    const filters = createFilterStore({
      createdAt: dateFilter({ label: 'Datum' }),
    });

    filters.set('createdAt', {
      operator: MatchModeCodes.GreaterThan,
      value: new Date('2026-09-01T00:00:00.000Z'),
    });
    filters.commit('createdAt');
    const before = filters.applied();

    filters.set('createdAt', {
      operator: MatchModeCodes.GreaterThan,
      value: new Date('2026-09-01T00:00:00.000Z'),
    });
    filters.commit('createdAt');

    expect(filters.applied()).toBe(before);
  });

  // The bail is on VALUE equality, so a changed operator over the same value still publishes:
  // "posle 1.9." and "pre 1.9." are different questions.
  it('the same value under a different operator still publishes', () => {
    const filters = createFilterStore({
      createdAt: dateFilter({ label: 'Datum' }),
    });

    filters.set('createdAt', {
      operator: MatchModeCodes.GreaterThan,
      value: new Date('2026-09-01T00:00:00.000Z'),
    });
    filters.commit('createdAt');
    const before = filters.applied();

    filters.set('createdAt', {
      operator: MatchModeCodes.LessThan,
      value: new Date('2026-09-01T00:00:00.000Z'),
    });
    filters.commit('createdAt');

    expect(filters.applied()).not.toBe(before);
    expect(filters.applied()[0].operator).toBe(MatchModeCodes.LessThan);
  });
});

// The same narrowing `Column.matchModes` gives a header filter, for the same reason: the full
// list can contain an operator that is legal on the wire but never the question. The forcing
// case is date-equality against a TIMESTAMP — it matches only the row written in that exact
// second and answers with an empty grid (PACMS, all three order-list date columns, Filip
// 2026-08-28) — so those columns must be able to offer "posle"/"pre" and nothing else.
describe('createFilterStore — narrowing the offered operators', () => {
  it('offers exactly the declared operators, in declaration order, first as default', () => {
    const filters = createFilterStore({
      createdAt: dateFilter({
        label: 'Datum',
        operators: [MatchModeCodes.GreaterThan, MatchModeCodes.LessThan],
      }),
    });
    const createdAt = filters.get('createdAt');

    expect(createdAt.operators.map((option) => option.value)).toEqual([
      MatchModeCodes.GreaterThan,
      MatchModeCodes.LessThan,
    ]);
    expect(createdAt.defaultOperator).toBe(MatchModeCodes.GreaterThan);
  });

  // Same polarity as the column's `computeMatchModes`: a bad entry is dropped and said out loud,
  // and a narrowing that leaves nothing falls back to the full list — an editor with no operator
  // options would be unusable, which is worse than an unwanted one.
  it('drops operators the kind does not allow, and falls back to the full list when none survive', () => {
    const errors = spyOn(console, 'error');

    const filters = createFilterStore({
      createdAt: dateFilter({
        label: 'Datum',
        operators: [MatchModeCodes.In],
      }),
    });

    expect(filters.get('createdAt').operators.map((option) => option.value)).toEqual([
      MatchModeCodes.Equals,
      MatchModeCodes.LessThan,
      MatchModeCodes.GreaterThan,
    ]);
    expect(errors).toHaveBeenCalled();
  });
});

// The placement API. A handle is bound to one filter and needs nothing from the component tree,
// which is what lets a filter be driven from a drawer or a modal rendered at app root — somewhere
// a directive could never reach. Its `value` is the DRAFT, because that is what a control shows:
// a text box displays what you typed, not what is applied.
describe('createFilterStore — per-filter handles', () => {
  it('hands out a handle whose value is the draft, separate from what the bar shows', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });
    const companyName = filters.get('companyName');

    expect(companyName.label).toBe('Firma');
    expect(companyName.value()).toBeUndefined();

    companyName.set({ operator: MatchModeCodes.Contains, value: 'Elektro' });

    expect(companyName.value()).toBe('Elektro');
    expect(filters.applied()).toEqual([]);

    companyName.commit();

    expect(filters.applied().length).toBe(1);
  });
});
