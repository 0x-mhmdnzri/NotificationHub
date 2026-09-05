'use client'

import Link from 'next/link'
import { PageHeader } from '@/components/page-header'
import { DeliveryFlowCanvas } from '@/components/flow/delivery-flow-canvas'
import { Button } from '@/components/ui/button'

export default function WorkflowLivePage() {
  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[1500px] space-y-6">
        <PageHeader
          title="جریان تحویل"
          description="حرکت اعلان‌ها از اپ تا افزونه تحویل را ببینید — صف، در حال ارسال، تحویل‌شده یا ناموفق."
          action={
            <Link href="/workflows">
              <Button variant="outline" size="sm">
                سازنده گردش‌کار
              </Button>
            </Link>
          }
        />
        <DeliveryFlowCanvas />
      </div>
    </div>
  )
}
