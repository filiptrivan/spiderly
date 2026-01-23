interface PropertySchema {
  type: string; // Use a string or an enum to represent the type (e.g., 'string', 'number', 'Date')
  nestedConstructor?: SchemaAwareConstructor<any>;
  isSaveBodyMainDTO?: boolean;
  // Add other metadata you might need (e.g., isRequired, minLength, etc.)
}

export interface ClassSchema {
  [propertyName: string]: PropertySchema;
}

// A type that represents a class constructor with a static 'schema' property
export type SchemaAwareConstructor<T> = {
  new (...args: any[]): T;
  readonly schema: ClassSchema; // The static property holding the schema
  readonly typeName: string; // 'typeName' can't be named 'name' because of the conflict with the Function.name
};

export class BaseEntity {}
