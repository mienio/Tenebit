import { categoryIconOptions } from '../utils/categoryIcons';

export function IconPicker({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <div className="iconGrid" role="listbox">
      {categoryIconOptions.map(item => (
        <button
          key={item.value}
          type="button"
          role="option"
          aria-selected={value === item.value}
          title={item.value}
          className={value === item.value ? 'iconGridButton iconGridButton--active' : 'iconGridButton'}
          onClick={() => onChange(item.value)}
        >
          <item.icon size={18} />
        </button>
      ))}
    </div>
  );
}
