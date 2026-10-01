---
description: Assign every unblocked, unassigned issue not awaiting a decision or a major version to the gh user, move it to In Progress, and implement each one as a PR from its own worktree via sub-agents.
argument-hint: "[issue numbers to restrict to] [--dry-run]"
allowed-tools: Bash(gh:*), Bash(git:*), Agent
---

# Implement unblocked issues

You are the **fanning agent** described under *Worktrees* in `AGENTS.md`. You provision one worktree
per issue up front, then hand each sub-agent a path that already exists. Sub-agents only do the
work. They never provision.

Arguments: `$ARGUMENTS`

* Bare numbers restrict the run to those issues. They must still pass the filter in step 2.
* `--dry-run` stops after step 2. Print the issues that would be picked up and change nothing.

The repository is `PaulTrampert/PTrampert.SimplePatch`.

## 1. Preflight

If any check fails, stop and tell the user how to fix it. Don't work around a failure.

* `gh auth status` must list the `project` scope, which moving an issue on a project board needs.
  If it's missing, ask the user to run `! gh auth refresh -s project`.
* Bring `main` up to date so every worktree starts from the latest `main`, not from whatever this
  clone last fetched. Find the primary checkout from the shared git directory so this works from
  any worktree. Then fetch and pin the commit the whole batch branches from:

  ```bash
  PRIMARY="$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
  git -C "$PRIMARY" fetch origin main
  BASE="$(git -C "$PRIMARY" rev-parse origin/main)"
  git -C "$PRIMARY" fetch . origin/main:main   # fast-forward local main; refuses if it has diverged
  ```

  If the fetch fails, stop. If git refuses the fast-forward because local `main` has commits that
  aren't on `origin/main`, stop and report those commits. Never reset `main`. If git refuses only
  because `main` is checked out in a worktree, and that tree is clean, run
  `git merge --ff-only origin/main` there. If it isn't clean, leave it and note it for the final
  report. Either way, the worktrees branch from `$BASE`.

## 2. Find the issues

An issue qualifies when all of the following hold:

* It is **open**.
* It has **no assignees**.
* It has **no open blocking issue** (GitHub issue dependencies). A closed blocker no longer blocks.
* It has **no open pull request** that will close it. Someone is already working on it.
* It isn't labelled **`needs decision`**, meaning it isn't defined well enough to implement yet.
* It isn't labelled **`Breaking Change`**, meaning it has to wait for a major version.

Naming an issue in the arguments doesn't override the label rules. The label has to be removed
first.

```bash
gh api graphql --paginate -f query='
query($endCursor: String) {
  repository(owner: "PaulTrampert", name: "PTrampert.SimplePatch") {
    issues(states: OPEN, first: 100, after: $endCursor) {
      pageInfo { hasNextPage endCursor }
      nodes {
        number
        title
        labels(first: 20) { nodes { name } }
        assignees(first: 1) { totalCount }
        blockedBy(first: 50) { nodes { number state } }
        closedByPullRequestsReferences(first: 10, includeClosedPrs: false) { nodes { number state } }
      }
    }
  }
}' --jq '.data.repository.issues.nodes[]
  | select(.assignees.totalCount == 0)
  | select([.labels.nodes[].name] | (index("needs decision") or index("Breaking Change")) | not)
  | select([.blockedBy.nodes[] | select(.state == "OPEN")] | length == 0)
  | select([.closedByPullRequestsReferences.nodes[] | select(.state == "OPEN")] | length == 0)
  | {number, title, labels: [.labels.nodes[].name]}'
```

Print the list with each issue's number and title. If the list is empty, say so and stop. If
`--dry-run` was given, stop here.

## 3. Claim them

For each issue:

1. Assign it to the account `gh` is logged in as:
   `gh issue edit <n> --repo PaulTrampert/PTrampert.SimplePatch --add-assignee @me`.
2. Move it to **In Progress** on every project board it belongs to. Look up the issue's project
   items and each project's `Status` field:

   ```bash
   gh api graphql -f query='
   query($n: Int!) {
     repository(owner: "PaulTrampert", name: "PTrampert.SimplePatch") {
       issue(number: $n) {
         projectItems(first: 10) {
           nodes {
             id
             project {
               id
               title
               field(name: "Status") {
                 ... on ProjectV2SingleSelectField { id options { id name } }
               }
             }
           }
         }
       }
     }
   }' -F n=<n>
   ```

   Then set the option whose name is `In Progress`, compared case-insensitively:

   ```bash
   gh project item-edit --id <item id> --project-id <project id> \
     --field-id <field id> --single-select-option-id <option id>
   ```

   If the issue isn't on any project, or a project has no `In Progress` option, note it for the
   final report and carry on. Don't add the issue to a project or invent a status.

Claim every issue before starting any implementation, so the board shows the whole batch at once.

## 4. Provision the worktrees

Worktrees go inside the primary checkout, under `.claude/worktrees/`, which git ignores. Never
create one beside the checkout. `$PRIMARY` and `$BASE` come from step 1:

```bash
WORKTREES="$PRIMARY/.claude/worktrees"
```

For each issue, pick a branch name. Use `bugfix/<n>-<slug>` if the issue has the `bug` label, and
`feature/<n>-<slug>` otherwise. `<slug>` is the title in lower case, reduced to `[a-z0-9-]`, and
cut to a few words. Then run:

```bash
git -C "$PRIMARY" worktree add "$WORKTREES/issue-<n>" -b <branch> "$BASE"
```

If the branch or the directory already exists, don't reuse or overwrite it. Skip that issue and
report it. Never switch the branch of an existing worktree.

## 5. Implement, one sub-agent per issue

Launch every sub-agent **in a single message** so they run concurrently. Use the `general-purpose`
agent type. Don't pass `isolation`, because the worktree already exists. Give each one this
prompt, filled in:

> You are implementing GitHub issue #<n> ("<title>") in `PaulTrampert/PTrampert.SimplePatch`.
>
> Work only in the worktree at `<absolute worktree path>`. It is already checked out on branch
> `<branch>`, from `main` at `<base commit>`. Use absolute paths or `git -C` for everything. Don't
> create another worktree and don't switch branches. If `git branch --show-current` there isn't
> `<branch>`, stop and report that.
>
> 1. Read `AGENTS.md` in the worktree and follow it. It holds the project's coding standards, test
>    policy, and PR conventions.
> 2. Read the issue in full: `gh issue view <n> --repo PaulTrampert/PTrampert.SimplePatch --comments`.
>    If it points to a design document under `docs/`, read that too. A design document on `main`
>    is settled, so implement it as written. If the issue can't be delivered as written, or needs a
>    decision that neither the issue nor the design doc makes, stop and report why. Don't guess,
>    and don't deviate from the design.
> 3. Implement the issue, with a regression test that fails before the fix. Keep the change scoped
>    to this issue. Any change to the public API must be additive. If the issue can't be done
>    without a breaking change, stop and report that.
> 4. `dotnet build` must pass with no new warnings, and `dotnet test` must pass.
> 5. Commit in meaningful steps. End each commit message with
>    `Co-Authored-By: Claude <noreply@anthropic.com>`.
> 6. Push with `git -C <path> push -u origin <branch>`, then open a PR against `main` with
>    `gh pr create`. Start the title with `(PATCH): `, `(MINOR): ` or `(MAJOR): `, as `AGENTS.md`
>    describes. The body explains what the diff doesn't make obvious, gives the test results,
>    includes `Closes #<n>`, and ends with
>    `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
>
> Finish with a short report: the PR URL (or why there isn't one), what you tested and how, and
> anything the reviewer should look at first.

If your own system prompt specifies a co-author attribution, use it in place of `Claude` in the
co-author line.

## 6. Report

When every sub-agent has finished, give the user one table showing each issue, its branch, and its
PR link or the reason there isn't one. Also list any issue that couldn't be moved to *In Progress*.
Leave the worktrees in place, because they hold the branches under review. Give the command to
remove one once its PR merges: `git worktree remove <path>`.

If a sub-agent fails, don't unassign its issue or move it back on the board. Report the failure and
leave the decision to the user.
