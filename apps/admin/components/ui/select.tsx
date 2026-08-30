import * as React from 'react'; import {cn} from '@/lib/utils'
export function Select({className,...props}:React.SelectHTMLAttributes<HTMLSelectElement>){return <select className={cn('h-10 rounded-lg border bg-background px-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/15',className)} {...props}/>} 
