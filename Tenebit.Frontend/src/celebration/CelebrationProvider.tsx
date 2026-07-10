import { CheckCircle2 } from 'lucide-react';
import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';

interface Particle {
  id: number;
  angle: number;
  distance: number;
  delay: number;
  color: string;
}

interface Celebration {
  id: number;
  message: string;
  particles: Particle[];
}

interface CelebrationContextValue {
  celebrate: (message: string) => void;
}

const CelebrationContext = createContext<CelebrationContextValue | null>(null);

const colors = ['#111827', '#d97706', '#047857', '#1d4ed8', '#be123c'];
const particleCount = 14;
const visibleMs = 1600;

function buildParticles(): Particle[] {
  return Array.from({ length: particleCount }, (_, index) => ({
    id: index,
    angle: (360 / particleCount) * index + (Math.random() * 20 - 10),
    distance: 46 + Math.random() * 34,
    delay: Math.random() * 80,
    color: colors[index % colors.length]
  }));
}

export function CelebrationProvider({ children }: { children: ReactNode }) {
  const [celebration, setCelebration] = useState<Celebration | null>(null);
  const timeoutRef = useRef<number | null>(null);
  const nextId = useRef(0);

  const celebrate = useCallback((message: string) => {
    if (timeoutRef.current) window.clearTimeout(timeoutRef.current);
    nextId.current += 1;
    setCelebration({ id: nextId.current, message, particles: buildParticles() });
    timeoutRef.current = window.setTimeout(() => setCelebration(null), visibleMs);
  }, []);

  const value = useMemo<CelebrationContextValue>(() => ({ celebrate }), [celebrate]);

  return (
    <CelebrationContext.Provider value={value}>
      {children}
      {celebration && (
        <div className="celebrationOverlay" aria-live="polite">
          <div className="celebrationBadge" key={celebration.id}>
            <CheckCircle2 size={20} />
            <span>{celebration.message}</span>
            <div className="celebrationBurst">
              {celebration.particles.map(particle => (
                <i
                  key={particle.id}
                  style={{
                    '--angle': `${particle.angle}deg`,
                    '--dist': `${particle.distance}px`,
                    '--delay': `${particle.delay}ms`,
                    '--color': particle.color
                  } as React.CSSProperties}
                />
              ))}
            </div>
          </div>
        </div>
      )}
    </CelebrationContext.Provider>
  );
}

export function useCelebration() {
  const context = useContext(CelebrationContext);
  if (!context) throw new Error('useCelebration musi być użyty wewnątrz CelebrationProvider.');
  return context;
}
