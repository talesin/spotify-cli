# React

A curated React style guide designed to help AI coding assistants like Claude, Copilot, and ChatGPT generate consistent, maintainable code.

## Based On

This guide is adapted from the [Airbnb React/JSX Style Guide](https://github.com/airbnb/javascript/tree/master/react), tailored for AI-first coding workflows.

## 1. Component Structure

- Always use **function components**.
- Prefer **arrow functions** unless a named function is explicitly required.
- Export only **one component per file**.
- **Match the file and component name**.

```tsx
// Good
export const Button = () => {
  return <button>Click me</button>;
};

// Bad
function Btn() {
  return <button>Click me</button>;
}
```

## 2. Props and Types

- Always define a `Props` type or interface, even if it's empty.
- Use `interface` unless you need unions or advanced types.

```tsx
interface ButtonProps {
  label: string;
  onClick: () => void;
}

export const Button: React.FC<ButtonProps> = ({ label, onClick }) => {
  return <button onClick={onClick}>{label}</button>;
};
```

## 3. Hooks

- Never call hooks conditionally.
- Extract complex `useEffect` logic into custom hooks.
- Prefer `useReducer` over multiple `useState` calls for complex state.

## 4. Styling

- Prefer **Tailwind CSS** or **CSS Modules**.
- Avoid inline styles unless dynamic and small.
- Descriptive class names if not using Tailwind.

## 5. Naming

- Use **descriptive PascalCase** names: `UserCard`, `SignInForm`, `NavBar`.
- Avoid vague or abbreviated names (`Uc`, `Sif`, `nb`).

## 6. Folder Structure

Organize components with their logic, styles, and tests.

```text
components/
  Button/
    Button.tsx
    Button.test.tsx
    Button.module.css
  Header/
    Header.tsx
```

## 7. Imports

- Group by type: **built-in**, **third-party**, **internal**.
- Alphabetize within groups.
- Prefer **absolute imports** using `tsconfig` paths.

```tsx
import React from "react";
import { useRouter } from "next/router";

import { Button } from "@/components/ui";
import { useAuth } from "@/hooks/useAuth";
```

## 8. File Conventions

- Use `.tsx` for all React components.
- Co-locate tests with components: `ComponentName.test.tsx`.

## 9. Code Formatting

- Use **Prettier** with standard configuration.
- Use ESLint with:
  - `eslint-plugin-react`
  - `eslint-plugin-jsx-a11y`
- Use no semicolons if following Prettier defaults.

## 10. AI-Specific Additions

- Add comments to **non-trivial** logic.
- Use markers for protected code:
  ```tsx
  // AI: DO NOT MODIFY
  ```
- Add short summaries at top of files:
  ```tsx
  // Renders a button that triggers a parent callback when clicked.
  // Used across the site for primary actions.
  ```

## Example Component

```tsx
// Renders a styled button with an onClick callback
// Used in primary user actions
interface ButtonProps {
  label: string;
  onClick: () => void;
}

export const Button: React.FC<ButtonProps> = ({ label, onClick }) => {
  return (
    <button
      onClick={onClick}
      className="rounded bg-blue-600 text-white px-4 py-2"
    >
      {label}
    </button>
  );
};
```

## Optional Tools

- Prettier: For consistent formatting.
- ESLint: For syntax and logic rules.
- TypeScript: Strongly recommended.
- Tailwind CSS: Encouraged for styling clarity and reuse.
- Testing Library + Jest: For unit and integration testing.
