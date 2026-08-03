#!/usr/bin/env node

// Rewrites the release block in README.md from the version in package.json.
// Run without arguments to update the file, or with --check to verify it is current.

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const START = '<!-- fynite-release:start -->';
const END = '<!-- fynite-release:end -->';

const SEMVER =
  /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const manifestPath = join(root, 'package.json');
const readmePath = join(root, 'README.md');

function fail(message) {
  console.error(`sync-readme-version: ${message}`);
  process.exit(1);
}

function read(path) {
  try {
    return readFileSync(path, 'utf8');
  } catch (error) {
    fail(`cannot read ${relative(root, path) || path}: ${error.message}`);
  }
}

function readVersion() {
  let manifest;

  try {
    manifest = JSON.parse(read(manifestPath));
  } catch (error) {
    fail(`package.json is not valid JSON: ${error.message}`);
  }

  const version = manifest.version;

  if (typeof version !== 'string' || !SEMVER.test(version)) {
    fail(`package.json has no valid semver version (found ${JSON.stringify(version)})`);
  }

  return version;
}

function locateBlock(readme) {
  const starts = readme.split(START).length - 1;
  const ends = readme.split(END).length - 1;

  if (starts === 0 || ends === 0) {
    fail(`README.md is missing the ${starts === 0 ? START : END} marker`);
  }

  if (starts > 1 || ends > 1) {
    fail(`README.md has ${starts} start and ${ends} end markers, expected one of each`);
  }

  const from = readme.indexOf(START);
  const to = readme.indexOf(END);

  if (to < from) {
    fail(`README.md has ${END} before ${START}`);
  }

  return { from, to: to + END.length };
}

function renderBlock(version, eol) {
  const tag = `v${version}`;

  return [
    START,
    `The latest published release is **${tag}**.`,
    '',
    'In the Unity Package Manager, choose *Add package from git URL* and paste:',
    '',
    '```',
    `https://github.com/Natteens/fynite.git#${tag}`,
    '```',
    '',
    'Or declare the dependency in `Packages/manifest.json`:',
    '',
    '```json',
    '{',
    '  "dependencies": {',
    `    "com.natteens.fynite": "https://github.com/Natteens/fynite.git#${tag}"`,
    '  }',
    '}',
    '```',
    END
  ].join(eol);
}

const args = process.argv.slice(2);
const unknown = args.filter((arg) => arg !== '--check');

if (unknown.length > 0) {
  fail(`unknown argument ${unknown[0]} (usage: sync-readme-version.mjs [--check])`);
}

const check = args.includes('--check');
const version = readVersion();
const readme = read(readmePath);
const { from, to } = locateBlock(readme);
const eol = readme.includes('\r\n') ? '\r\n' : '\n';
const updated = readme.slice(0, from) + renderBlock(version, eol) + readme.slice(to);

if (updated === readme) {
  console.log(`README.md is up to date with v${version}`);
  process.exit(0);
}

if (check) {
  console.error(`sync-readme-version: README.md does not match package.json (expected v${version})`);
  process.exit(1);
}

writeFileSync(readmePath, updated);
console.log(`README.md updated to v${version}`);
