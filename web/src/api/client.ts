import type { ZodType } from 'zod'
import {
  decisionSchema,
  demoIncidentSchema,
  healthSchema,
  verificationProbeSchema,
  type Decision,
  type DemoIncident,
  type Health,
  type VerificationProbe,
} from './schema'

const BASE_URL = (import.meta.env.VITE_FORKCAST_API ?? 'http://localhost:5199').replace(/\/$/, '')

/** An error already phrased for a person, not a console. */
export class ForkcastError extends Error {
  readonly detail?: string

  constructor(message: string, detail?: string) {
    super(message)
    this.name = 'ForkcastError'
    this.detail = detail
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
}

async function request<T>(path: string, schema: ZodType<T>, init?: RequestInit): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch {
    throw new ForkcastError(
      'Cannot reach the Forkcast API.',
      `No response from ${BASE_URL}. Start the API with: dotnet run --project src/Forkcast.Api`,
    )
  }

  if (!response.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // A non-JSON error body is still an error; the status carries enough meaning.
    }

    throw new ForkcastError(
      problem.title ?? `The API returned ${response.status}.`,
      problem.detail,
    )
  }

  const parsed = schema.safeParse(await response.json())
  if (!parsed.success) {
    // Rendering a response we could not fully understand would put unverified values on a
    // screen whose entire promise is that its values are verified.
    throw new ForkcastError(
      'The API returned a response Forkcast does not recognise.',
      parsed.error.issues
        .slice(0, 3)
        .map((issue) => `${issue.path.join('.')}: ${issue.message}`)
        .join('; '),
    )
  }

  return parsed.data
}

export const api = {
  baseUrl: BASE_URL,

  health: () => request('/api/health', healthSchema),

  demoIncident: (): Promise<DemoIncident> => request('/api/demo/incident', demoIncidentSchema),

  run: (narrative?: string): Promise<Decision> =>
    request('/api/simulations/run', decisionSchema, {
      method: 'POST',
      body: JSON.stringify({ narrative }),
    }),

  challenge: (question: string, narrative?: string): Promise<Decision> =>
    request('/api/simulations/challenge', decisionSchema, {
      method: 'POST',
      body: JSON.stringify({ question, narrative }),
    }),

  probe: (submitted: string, narrative?: string): Promise<VerificationProbe> =>
    request('/api/verification/probe', verificationProbeSchema, {
      method: 'POST',
      body: JSON.stringify({ submitted, narrative }),
    }),
}

export type { Decision, DemoIncident, Health }
