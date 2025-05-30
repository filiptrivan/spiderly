import { BaseEntity } from "../entities/base-entity";

export class PrimengOption extends BaseEntity
{
    label?: string;
    code?: any; // Can't be value: https://github.com/primefaces/primeng/issues/17332#issuecomment-2922861294
  
    constructor(
    {
        label,
        code,
    }:{
        label?: string;
        code?: any;
    } = {}
    ) {
        super('PrimengOption');

        this.label = label;
        this.code = code;
    }

}