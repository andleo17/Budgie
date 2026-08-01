import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import client from "@/lib/api-client";
import type { components } from "@/lib/api-types";

type CategoryResponse = components["schemas"]["CategoryResponse"];

export function useCategories() {
  return useQuery({
    queryKey: ["categories"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/finances/categories");
      if (error) throw error;
      return data;
    },
  });
}

export interface TreeNode extends CategoryResponse {
  children: TreeNode[];
}

function buildTree(categories: CategoryResponse[]): TreeNode[] {
  const map = new Map<string, TreeNode>();
  const roots: TreeNode[] = [];

  for (const c of categories) {
    map.set(c.id, { ...c, children: [] });
  }

  for (const node of map.values()) {
    if (node.parentId && map.has(node.parentId)) {
      map.get(node.parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }

  return roots;
}

function flattenTree(nodes: TreeNode[], depth = 0): { node: TreeNode; depth: number }[] {
  const result: { node: TreeNode; depth: number }[] = [];
  for (const node of nodes) {
    result.push({ node, depth });
    if (node.children.length > 0) {
      result.push(...flattenTree(node.children, depth + 1));
    }
  }
  return result;
}

export function useCategoryTree(categories: CategoryResponse[] | undefined) {
  return useMemo(() => {
    if (!categories) return [];
    const tree = buildTree(categories);
    return flattenTree(tree);
  }, [categories]);
}
