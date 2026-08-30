'use client'
import {useState} from 'react'
import {Loader2, Play, Save, RotateCcw} from 'lucide-react'
import {Card,CardContent,CardHeader,CardTitle} from '@/components/ui/card'
import {Button} from '@/components/ui/button'
import {Input} from '@/components/ui/input'
import {Textarea} from '@/components/ui/textarea'
import {Badge} from '@/components/ui/badge'
import {ToastHost} from '@/components/toast-host'
import {ApiError} from '@/lib/api/errors'

export function OperationPanel({title,description,children,onSubmit,label='Save'}:{title:string;description?:string;children:React.ReactNode;onSubmit:()=>Promise<unknown>;label?:string}){
 const [busy,setBusy]=useState(false); const [toast,setToast]=useState<any>(null)
 async function submit(){setBusy(true);setToast(null);try{await onSubmit();setToast({tone:'success',title:'Operation completed',description:'The NotificationHub API accepted the request.'})}catch(e){setToast({tone:'error',title:'Operation failed',description:e instanceof ApiError?e.message:'Unable to reach the API.'})}finally{setBusy(false)}}
 return <><ToastHost toast={toast} onClose={()=>setToast(null)}/><Card className="overflow-hidden"><CardHeader className="border-b bg-muted/20"><div className="flex items-center justify-between gap-3"><div><CardTitle>{title}</CardTitle>{description&&<p className="mt-1 text-xs text-muted-foreground">{description}</p>}</div><Badge variant="outline">API</Badge></div></CardHeader><CardContent className="space-y-5 p-6">{children}<div className="flex justify-end border-t pt-4"><Button onClick={submit} disabled={busy}>{busy?<Loader2 size={15} className="animate-spin"/>:<Save size={15}/>} {busy?'Working…':label}</Button></div></CardContent></Card></>
}
export function Field({label,hint,children}:{label:string;hint?:string;children:React.ReactNode}){return <label className="block space-y-2"><span className="flex gap-2 text-sm font-medium">{label}{hint&&<span className="text-[10px] font-normal text-muted-foreground">{hint}</span>}</span>{children}</label>}
export function JsonArea({value,onChange}:{value:string;onChange:(v:string)=>void}){return <Textarea value={value} onChange={e=>onChange(e.target.value)} className="min-h-40 font-mono text-xs" spellCheck={false}/>} 
