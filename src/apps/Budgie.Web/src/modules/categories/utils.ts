import type { components } from "@/lib/api-types";

type CategoryResponse = components["schemas"]["CategoryResponse"];

export interface CategoryTreeItem {
  id: string;
  name: string;
  depth: number;
  isSystem?: boolean;
  path: string;
}

export function buildCategoryTree(
  categories: CategoryResponse[],
  options?: {
    excludeCategoryId?: string;
    includeSystem?: boolean;
    type?: number;
    forParent?: boolean;
  }
): CategoryTreeItem[] {
  let filtered = categories;

  if (options?.type !== undefined) {
    filtered = filtered.filter((c) => c.type === options.type);
  }

  if (!options?.includeSystem) {
    filtered = filtered.filter((c) => !c.isSystem);
  }

  const catMap = new Map(filtered.map((c) => [c.id, c]));

  const depths = new Map<string, number>();
  function calcDepth(id: string): number {
    if (!catMap.has(id)) return 0;
    const cached = depths.get(id);
    if (cached !== undefined) return cached;
    const cat = catMap.get(id)!;
    if (!cat.parentId) {
      depths.set(id, 0);
      return 0;
    }
    const d = calcDepth(cat.parentId) + 1;
    depths.set(id, d);
    return d;
  }
  for (const c of filtered) calcDepth(c.id);

  const excludedIds = new Set<string>();
  if (options?.excludeCategoryId) {
    excludedIds.add(options.excludeCategoryId);
    const queue = [options.excludeCategoryId];
    for (const id of queue) {
      for (const c of filtered) {
        if (c.parentId === id && !excludedIds.has(c.id)) {
          excludedIds.add(c.id);
          queue.push(c.id);
        }
      }
    }
  }

  const available = filtered.filter((c) => {
    if (excludedIds.has(c.id)) return false;
    if (options?.forParent && (depths.get(c.id) ?? 0) >= 2) return false;
    return true;
  });

  const items: CategoryTreeItem[] = [];

  function getPath(cat: CategoryResponse): string {
    const parts: string[] = [cat.name];
    let current = cat.parentId ? catMap.get(cat.parentId) : undefined;
    while (current) {
      parts.unshift(current.name);
      current = current.parentId ? catMap.get(current.parentId) : undefined;
    }
    return parts.join("/");
  }

  const roots = available
    .filter((c) => !c.parentId)
    .sort((a, b) => a.name.localeCompare(b.name));

  function addChildren(parentId: string, depth: number) {
    const children = available
      .filter((c) => c.parentId === parentId)
      .sort((a, b) => a.name.localeCompare(b.name));
    for (const child of children) {
      items.push({
        id: child.id,
        name: child.name,
        depth,
        isSystem: child.isSystem,
        path: getPath(child),
      });
      addChildren(child.id, depth + 1);
    }
  }

  for (const root of roots) {
    items.push({
      id: root.id,
      name: root.name,
      depth: 0,
      isSystem: root.isSystem,
      path: root.name,
    });
    addChildren(root.id, 1);
  }

  return items;
}
