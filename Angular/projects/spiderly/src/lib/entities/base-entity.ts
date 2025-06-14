export class BaseEntity {

    public typeName?: string;

    constructor(typeName: string) {
        this.typeName = typeName;
    }
}