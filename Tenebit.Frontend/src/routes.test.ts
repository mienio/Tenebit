import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { nav } from './components/Layout';

const here = dirname(fileURLToPath(import.meta.url));
const appSource = readFileSync(join(here, 'App.tsx'), 'utf8');

/** Wszystkie `path="..."` z App.tsx, znormalizowane do postaci z wiodacym ukosnikiem. */
function declaredPaths(): string[] {
  return [...appSource.matchAll(/<Route\s+path="([^"]+)"/g)]
    .map((m) => m[1])
    .filter((p) => p !== '*')
    .map((p) => (p.startsWith('/') ? p : '/' + p));
}

describe('routing', () => {
  // Regresja: `/audit` bylo zadeklarowane dwa razy - jako publiczna strona kampanii
  // inwentaryzacyjnej i jako dziennik zdarzen. Publiczna wygrywala dopasowanie, wiec dziennik
  // byl nieosiagalny mimo pozycji w menu.
  it('nie deklaruje dwa razy tej samej sciezki', () => {
    const paths = declaredPaths();
    const seen = new Map<string, number>();
    for (const p of paths) seen.set(p, (seen.get(p) ?? 0) + 1);
    const duplikaty = [...seen.entries()].filter(([, n]) => n > 1).map(([p]) => p);
    expect(duplikaty).toEqual([]);
  });

  // Kazda pozycja menu musi prowadzic do istniejacej trasy, inaczej klikniecie konczy sie na 404.
  it('kazda pozycja menu ma swoja trase', () => {
    const paths = new Set(declaredPaths());
    const osierocone = nav.map((item) => item.to).filter((to) => !paths.has(to));
    expect(osierocone).toEqual([]);
  });

  // RequireRoles bierze role z `nav` albo z jawnego `roles`. Sciezka bez jednego i drugiego kiedys
  // cicho przepuszczala kazdego (`nav.find` zwracalo undefined), wiec kazdy wpis musi miec zrodlo rol.
  it('kazdy RequireRoles ma zrodlo uprawnien', () => {
    const znane = new Set(nav.map((item) => item.to));
    const bezRol = [...appSource.matchAll(/<RequireRoles\s+path="([^"]+)"(\s+roles=\{)?/g)]
      .filter((m) => !m[2] && !znane.has(m[1]))
      .map((m) => m[1]);
    expect(bezRol).toEqual([]);
  });
});
