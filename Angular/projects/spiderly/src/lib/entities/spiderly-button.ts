export interface SpiderlyButton
{
    label?: string;
    icon?: string;
    disabled?: boolean;
    onClick?: () => void;
    outlined?: boolean;
    severity?: 'success' | 'info' | 'warn' | 'danger' | 'help' | 'primary' | 'secondary' | 'contrast' | null | undefined;
    rounded?: boolean;
    size?: 'small' | 'large' | undefined;
}