import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import * as fs from "node:fs";
import * as path from "node:path";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Recursively collect all `.cs` files under `dir`. */
function findCsFiles(dir: string): string[] {
  const results: string[] = [];
  if (!fs.existsSync(dir)) return results;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      // Skip common non-source directories
      if (
        entry.name === "node_modules" ||
        entry.name === ".git" ||
        entry.name === "bin" ||
        entry.name === "obj" ||
        entry.name === "Library" ||
        entry.name === "Temp"
      ) {
        continue;
      }
      results.push(...findCsFiles(full));
    } else if (entry.name.endsWith(".cs")) {
      results.push(full);
    }
  }
  return results;
}

/** Read all `.cs` files and return `{ filePath, content }` pairs. */
function readAllCsFiles(root: string): Array<{ filePath: string; content: string }> {
  return findCsFiles(root).map((filePath) => ({
    filePath,
    content: fs.readFileSync(filePath, "utf-8"),
  }));
}

/** Resolve the scan root from an optional `path` argument. */
function resolveRoot(scanPath?: string): string {
  if (scanPath) return path.resolve(scanPath);
  // Default: two levels up from this file's directory
  // .claude/mcp-servers/hsm-analyzer/src/index.ts -> repo root
  return path.resolve(import.meta.dirname, "..", "..", "..", "..");
}

// ---------------------------------------------------------------------------
// Tree rendering
// ---------------------------------------------------------------------------

interface TreeNode {
  name: string;
  children: TreeNode[];
}

function renderTree(roots: TreeNode[], prefix = ""): string {
  const lines: string[] = [];
  for (let i = 0; i < roots.length; i++) {
    const node = roots[i];
    const isLast = i === roots.length - 1;
    const connector = isLast ? "└── " : "├── ";
    const childPrefix = isLast ? "    " : "│   ";

    if (prefix === "") {
      // Root-level node, no connector
      lines.push(node.name);
    } else {
      lines.push(prefix + connector + node.name);
    }

    if (node.children.length > 0) {
      const nextPrefix = prefix === "" ? "" : prefix + childPrefix;
      lines.push(renderTree(node.children, nextPrefix === "" ? "" : nextPrefix));
    }
  }
  return lines.join("\n");
}

function buildTree(parentMap: Map<string, string | null>): TreeNode[] {
  // Build children map
  const childrenOf = new Map<string, string[]>();
  const allNames = new Set<string>();

  for (const [child, parent] of parentMap) {
    allNames.add(child);
    if (parent) allNames.add(parent);

    const parentKey = parent ?? "__root__";
    if (!childrenOf.has(parentKey)) childrenOf.set(parentKey, []);
    childrenOf.get(parentKey)!.push(child);
  }

  function toTreeNode(name: string): TreeNode {
    const kids = (childrenOf.get(name) ?? []).sort();
    return { name, children: kids.map(toTreeNode) };
  }

  const roots = (childrenOf.get("__root__") ?? []).sort();
  return roots.map(toTreeNode);
}

function renderTreePretty(roots: TreeNode[]): string {
  const lines: string[] = [];

  function walk(nodes: TreeNode[], prefix: string) {
    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      const isLast = i === nodes.length - 1;
      const connector = isLast ? "└── " : "├── ";
      const childPrefix = isLast ? "    " : "│   ";

      lines.push(prefix + connector + node.name);
      if (node.children.length > 0) {
        walk(node.children, prefix + childPrefix);
      }
    }
  }

  for (const root of roots) {
    lines.push(root.name);
    walk(root.children, "");
  }

  return lines.join("\n");
}

// ---------------------------------------------------------------------------
// Tool implementations
// ---------------------------------------------------------------------------

function hsmStateTree(scanPath?: string): string {
  const root = resolveRoot(scanPath);
  const files = readAllCsFiles(root);

  // Map: className -> parentClassName | null
  const parentMap = new Map<string, string | null>();

  // Regex: [ParentState(typeof(XXX))] followed by a class declaration
  // Also handles [ParentState(null)]
  const attrRegex = /\[ParentState\((?:typeof\((\w+)\)|null)\)\]\s*(?:\[[\w()., ]*\]\s*)*(?:public\s+)?(?:partial\s+)?class\s+(\w+)/g;

  for (const { content } of files) {
    let match: RegExpExecArray | null;
    while ((match = attrRegex.exec(content)) !== null) {
      const parentName = match[1] ?? null; // null when [ParentState(null)]
      const className = match[2];
      parentMap.set(className, parentName);
    }
  }

  if (parentMap.size === 0) {
    return "No states with [ParentState] attribute found.";
  }

  const roots = buildTree(parentMap);
  return renderTreePretty(roots);
}

function hsmTransitions(scanPath?: string): string {
  const root = resolveRoot(scanPath);
  const files = readAllCsFiles(root);

  const results: string[] = [];

  // [Transition(typeof(Source), typeof(Target))] ... class ClassName
  const regex =
    /\[Transition\(typeof\((\w+)\),\s*typeof\((\w+)\)\)\]\s*(?:\[[\w()., ]*\]\s*)*(?:public\s+)?(?:partial\s+)?class\s+(\w+)/g;

  for (const { content } of files) {
    let match: RegExpExecArray | null;
    while ((match = regex.exec(content)) !== null) {
      const source = match[1];
      const target = match[2];
      const className = match[3];
      results.push(`${className}: ${source} → ${target}`);
    }
  }

  if (results.length === 0) {
    return "No transitions with [Transition] attribute found.";
  }

  return results.sort().join("\n");
}

function hsmExtensions(scanPath?: string): string {
  const root = resolveRoot(scanPath);
  const files = readAllCsFiles(root);

  const results: string[] = [];

  // [ExtensionFor(typeof(A), typeof(B), ...)] ... class ClassName
  // The attribute accepts params Type[], so we need to capture the whole typeof list
  const regex =
    /\[ExtensionFor\(((?:typeof\(\w+\)(?:,\s*)?)+)\)\]\s*(?:\[[\w()., ]*\]\s*)*(?:public\s+)?(?:partial\s+)?class\s+(\w+)/g;
  const typeofRegex = /typeof\((\w+)\)/g;

  for (const { content } of files) {
    let match: RegExpExecArray | null;
    while ((match = regex.exec(content)) !== null) {
      const typeofList = match[1];
      const className = match[2];
      const states: string[] = [];

      let typeMatch: RegExpExecArray | null;
      while ((typeMatch = typeofRegex.exec(typeofList)) !== null) {
        states.push(typeMatch[1]);
      }

      results.push(`${className}: compatible with ${states.join(", ")}`);
    }
  }

  if (results.length === 0) {
    return "No extensions with [ExtensionFor] attribute found.";
  }

  return results.sort().join("\n");
}

function hsmControllers(scanPath?: string): string {
  const root = resolveRoot(scanPath);
  const files = readAllCsFiles(root);

  const results: string[] = [];

  // Match class declarations that implement interfaces ending in "Controller"
  // e.g. public class TestController : IUpdateController, IEnterController
  const regex =
    /(?:public\s+)?(?:partial\s+)?class\s+(\w+)\s*:\s*((?:[\w<>,\s]+))/g;

  for (const { content } of files) {
    let match: RegExpExecArray | null;
    while ((match = regex.exec(content)) !== null) {
      const className = match[1];
      const inheritance = match[2];

      // Extract all I*Controller interfaces from the inheritance list
      const interfaces: string[] = [];
      const ifaceRegex = /\bI\w*Controller\b/g;
      let ifaceMatch: RegExpExecArray | null;
      while ((ifaceMatch = ifaceRegex.exec(inheritance)) !== null) {
        interfaces.push(ifaceMatch[0]);
      }

      if (interfaces.length > 0) {
        results.push(`${className}: ${interfaces.join(", ")}`);
      }
    }
  }

  if (results.length === 0) {
    return "No classes implementing IController-derived interfaces found.";
  }

  return results.sort().join("\n");
}

// ---------------------------------------------------------------------------
// MCP Server setup
// ---------------------------------------------------------------------------

const server = new McpServer({
  name: "hsm-analyzer",
  version: "1.0.0",
});

const pathSchema = {
  path: z
    .string()
    .optional()
    .describe(
      "Directory to scan for .cs files. Defaults to the repository root (two levels up from the MCP server directory)."
    ),
};

server.tool(
  "hsm_state_tree",
  "Scans C# source files for [ParentState] attributes and builds a hierarchical state tree. Shows the parent-child relationships between HSM states.",
  pathSchema,
  async ({ path: scanPath }) => ({
    content: [{ type: "text" as const, text: hsmStateTree(scanPath) }],
  })
);

server.tool(
  "hsm_transitions",
  "Scans C# source files for [Transition(typeof(Source), typeof(Target))] attributes. Lists all declared state transitions with their source and target states.",
  pathSchema,
  async ({ path: scanPath }) => ({
    content: [{ type: "text" as const, text: hsmTransitions(scanPath) }],
  })
);

server.tool(
  "hsm_extensions",
  "Scans C# source files for [ExtensionFor(typeof(State), ...)] attributes. Lists extension states and which host states they are compatible with.",
  pathSchema,
  async ({ path: scanPath }) => ({
    content: [{ type: "text" as const, text: hsmExtensions(scanPath) }],
  })
);

server.tool(
  "hsm_controllers",
  "Scans C# source files for classes implementing IController-derived interfaces (IUpdateController, IEnterController, IExitController, etc.). Lists each controller class with its implemented controller interfaces.",
  pathSchema,
  async ({ path: scanPath }) => ({
    content: [{ type: "text" as const, text: hsmControllers(scanPath) }],
  })
);

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((err) => {
  console.error("HSM Analyzer MCP server failed to start:", err);
  process.exit(1);
});
