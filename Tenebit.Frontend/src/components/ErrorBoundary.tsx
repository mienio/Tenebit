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

  render() {
    if (!this.state.hasError) return this.props.children;
    return (
      <div className="stateBox stateBox--error" role="alert">
        <AlertCircle size={30} />
        <h2>{this.props.title}</h2>
        <p>{this.props.description}</p>
        <button className="button button--secondary" type="button" onClick={() => this.setState({ hasError: false })}>{this.props.retryLabel}</button>
      </div>
    );
  }
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  return <Boundary title={t('errors.boundaryTitle')} description={t('errors.boundaryDesc')} retryLabel={t('errors.boundaryRetry')}>{children}</Boundary>;
}
