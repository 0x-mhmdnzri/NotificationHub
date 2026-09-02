import * as React from 'react'
import { cn } from '@/lib/utils'

export function Table({ className, ...props }: React.HTMLAttributes<HTMLTableElement>) {
  return (
    <div className="w-full overflow-auto">
      <table className={cn('w-full text-sm', className)} {...props} />
    </div>
  )
}

export const TableHeader = (p: React.HTMLAttributes<HTMLTableSectionElement>) => (
  <thead className="border-b text-muted-foreground" {...p} />
)

export const TableBody = (p: React.HTMLAttributes<HTMLTableSectionElement>) => <tbody {...p} />

export const TableRow = (p: React.HTMLAttributes<HTMLTableRowElement>) => (
  <tr className="border-b transition hover:bg-muted/40" {...p} />
)

export const TableHead = (p: React.ThHTMLAttributes<HTMLTableCellElement>) => (
  <th className="h-11 px-4 text-start text-xs font-medium uppercase tracking-wide" {...p} />
)

export const TableCell = (p: React.TdHTMLAttributes<HTMLTableCellElement>) => (
  <td className="px-4 py-3.5" {...p} />
)
