import { Component, type ReactNode } from 'react';
import { AlertCircle } from 'lucide-react';
import { useI18n } from '../i18n/I18nProvider';

type BoundaryProps = { title: string; description: string; retryLabel: string; children: ReactNode };
type BoundaryState = { hasError: boolean };

class Boundary extends Component<BoundaryProps, BoundaryState> {
  state: BoundaryState = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: unknown) {
    console.error('Unhandled render error:', error);
  }

  // A full reload, not setState({ hasError: false }). Clearing the flag re-renders the very same element
  // tree that just threw, so the button only ever appeared to do nothing - most often because the render
  // failed on a chunk left behind by a deploy, which React.lazy then refuses to re-fetch for the lifetime
  // of the document (see utils/lazyRoute.ts). Reloading is what the user was doing by hand with F5.
  render() {
    if (!this.state.hasError) return this.props.children;
    return (
      <div className="stateBox stateBox--error" role="alert">
        <AlertCircle size={30} />
        <h2>{this.props.title}</h2>
        <p>{this.props.description}</p>
        <button className="button button--secondary" type="button" onClick={() => window.location.reload()}>{this.props.retryLabel}</button>
      </div>
    );
  }
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  return <Boundary title={t('errors.boundaryTitle')} description={t('errors.boundaryDesc')} retryLabel={t('errors.boundaryRetry')}>{children}</Boundary>;
}
