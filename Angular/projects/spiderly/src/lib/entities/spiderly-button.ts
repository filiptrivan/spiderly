import { BaseEntity } from "../entities/base-entity";

export class SpiderlyButton extends BaseEntity
{
    label?: string;
    icon?: string;
    disabled?: boolean;
    onClick?: () => void;
  
    constructor(
    {
        label,
        icon,
        disabled,
        onClick,
    }:{
        label?: string;
        icon?: string;
        disabled?: boolean;
        onClick?: () => void;
    } = {}
    ) {
        super('SpiderlyButton');

        this.label = label;
        this.icon = icon;
        this.disabled = disabled;
        this.onClick = onClick;
    }
}