'use client'
import { X } from 'lucide-react'
import { motion, AnimatePresence } from 'framer-motion'
import { cn } from '@/lib/utils'

export function Dialog({ open, onOpenChange, title, description, children, className }: { open: boolean; onOpenChange: (v:boolean)=>void; title:string; description?:string; children:React.ReactNode; className?:string }) {
  return <AnimatePresence>{open && <div className="fixed inset-0 z-[80] flex items-center justify-center p-4" role="dialog" aria-modal="true"><motion.button aria-label="Close" className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={()=>onOpenChange(false)} initial={{opacity:0}} animate={{opacity:1}} exit={{opacity:0}}/><motion.div initial={{opacity:0,scale:.97,y:12}} animate={{opacity:1,scale:1,y:0}} exit={{opacity:0,scale:.97,y:12}} transition={{duration:.18}} className={cn('relative z-10 max-h-[90vh] w-full overflow-auto rounded-2xl border bg-card shadow-2xl',className)}><div className="sticky top-0 z-10 flex items-start justify-between border-b bg-card/95 p-5 backdrop-blur"><div><h2 className="text-lg font-semibold tracking-tight">{title}</h2>{description&&<p className="mt-1 text-xs leading-5 text-muted-foreground">{description}</p>}</div><button onClick={()=>onOpenChange(false)} className="rounded-lg p-2 text-muted-foreground transition hover:bg-muted hover:text-foreground"><X size={17}/></button></div>{children}</motion.div></div>}</AnimatePresence>
}
