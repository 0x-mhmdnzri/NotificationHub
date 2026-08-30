import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'
const buttonVariants = cva('inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 active:scale-[.98]', { variants:{variant:{default:'bg-primary text-primary-foreground shadow-sm hover:brightness-105',outline:'border bg-background hover:bg-muted',secondary:'bg-secondary text-secondary-foreground hover:bg-secondary/80',ghost:'hover:bg-muted',destructive:'bg-red-500 text-white hover:bg-red-600'},size:{default:'h-10 px-4 py-2',sm:'h-9 px-3',lg:'h-11 px-5',icon:'h-10 w-10'}}, defaultVariants:{variant:'default',size:'default'} })
export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants>{asChild?:boolean}
export const Button = React.forwardRef<HTMLButtonElement,ButtonProps>(({className,variant,size,asChild=false,...props},ref)=>{const Comp=asChild?Slot:'button';return <Comp ref={ref} className={cn(buttonVariants({variant,size,className}))} {...props}/>})
Button.displayName='Button'
