'use client'

import Link from 'next/link'
import { PageHeader } from '@/components/page-header'
import { DeliveryFlowCanvas } from '@/components/flow/delivery-flow-canvas'
import { Button } from '@/components/ui/button'

export default function WorkflowLivePage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Delivery flow"
        description="Watch notifications move from your app through the delivery plugin — queue, in-flight, delivered, or failed — with a plain-language log."
        action={
          <Link href="/workflows">
            <Button variant="outline" size="sm">
              Workflow builder
            </Button>
          </Link>
        }
      />
      <DeliveryFlowCanvas />
    </div>
  )
}
