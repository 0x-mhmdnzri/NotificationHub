import * as React from 'react'; import {cn} from '@/lib/utils'
export function Card({className,...props}:React.HTMLAttributes<HTMLDivElement>){return <div className={cn('rounded-2xl border bg-card text-card-foreground shadow-sm',className)} {...props}/>}
export function CardHeader({className,...props}:React.HTMLAttributes<HTMLDivElement>){return <div className={cn('flex flex-col gap-1.5 p-5',className)} {...props}/>}
export function CardTitle({className,...props}:React.HTMLAttributes<HTMLHeadingElement>){return <h3 className={cn('text-sm font-semibold tracking-tight',className)} {...props}/>}
export function CardContent({className,...props}:React.HTMLAttributes<HTMLDivElement>){return <div className={cn('p-5 pt-0',className)} {...props}/>}
