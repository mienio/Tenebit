import './processShim';
import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { AuthProvider } from './auth/AuthProvider';
import { ErrorBoundary } from './components/ErrorBoundary';
import { CelebrationProvider } from './celebration/CelebrationProvider';
import { I18nProvider } from './i18n/I18nProvider';
import './styles/index.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <I18nProvider>
        <ErrorBoundary>
          <AuthProvider>
            <CelebrationProvider>
              <App />
            </CelebrationProvider>
          </AuthProvider>
        </ErrorBoundary>
      </I18nProvider>
    </BrowserRouter>
  </React.StrictMode>
);
