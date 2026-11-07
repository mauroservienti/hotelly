---
name: Complete repository configuration
about: Describe the work required to complete the repository configuration
title: Complete repository configuration
labels: ''
assignees: mauroservienti
---

### Content

- [ ] Source code first commit in `src` or remove/adjust dependabot config for `src`
- [ ] assets: icon
- [ ] readme
- [ ] fix the Feedz.io link in the `package-release.md` issue template
- [ ] Delete `complete-repository-configuration.md` issue template

### CI

- [ ] Complete push to Feedz.io configuration
- [ ] Add `FEEDZ_API_KEY` secret

#### NuGet.org trusted publishing

- [ ] On NuGet.org, go to your username > `Trusted Publishing` and add a GitHub Actions policy with:
  - Repository owner: the owner of this repository
  - Repository: the name of this repository
  - Workflow file: `ci.yml`
  - Environment: leave empty
- [ ] Add `NUGET_USER` secret, set to the NuGet.org username (profile name, not the email address) that owns the policy

### Repository settings:

- [ ] assets: repo image
- [ ] branches protection
- [ ] disable Wikis
- [ ] disable Projects
- [ ] Automatically delete head branches
- [ ] Select `Allow merge commits`
- [ ] Select `Allow squash merging`
- [ ] Select `Allow rebase merging`
- [ ] Select `Allow auto-merge`
