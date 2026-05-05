import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="py-24 text-center space-y-3">
      <div className="text-3xl font-semibold text-slate-800">404</div>
      <p className="text-slate-500">The page you're looking for doesn't exist.</p>
      <Link to="/" className="btn-primary inline-flex">Back to dashboard</Link>
    </div>
  )
}
