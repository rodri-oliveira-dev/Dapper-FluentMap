# Community discussions

[GitHub Discussions](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/discussions) is the place to ask usage questions, exchange ideas, and share examples related to Dapper FluentMap.

## Where should I post?

| Category | Use it for | Form |
| --- | --- | --- |
| Q&A | Usage questions, package configuration, troubleshooting, and integrations | [q-a.yml](DISCUSSION_TEMPLATE/q-a.yml) |
| Ideas | Exploring potential features, API improvements, and documentation enhancements | [ideas.yml](DISCUSSION_TEMPLATE/ideas.yml) |
| Show and Tell | Community examples, patterns, and public integrations | [show-and-tell.yml](DISCUSSION_TEMPLATE/show-and-tell.yml) |
| General | Project-related conversations that do not fit the other categories | [general.yml](DISCUSSION_TEMPLATE/general.yml) |
| Announcements | Release notes and maintainer communications | Maintainer-authored posts; no form required |

Please search the [documentation](../README.md#documentation), [existing discussions](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/discussions), and [existing issues](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/issues) before opening a new thread.

- **Reproducible bugs:** [Open an issue](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/issues/new/choose) with a minimal reproduction, affected package/version, expected and actual behavior.
- **Questions:** Choose Q&A and include the package, version, relevant code, and database provider when applicable.
- **Proposals:** Choose Ideas, explain the problem first, and mention backward-compatibility concerns where relevant.
- **Examples:** Choose Show and Tell. Only publish code and data you are permitted to share.
- Be respectful and stay on topic. Never include secrets, access tokens, credentials, or private data.

## Maintainer setup and activation

The four YAML files in the DISCUSSION_TEMPLATE directory define category-specific forms, **not** the categories themselves.

1. In the repository's Discussions settings, verify that the categories **Q&A**, **Ideas**, **Show and Tell**, and **General** exist. Create any that are missing.
2. Use the question-and-answer format for Q&A so replies can be marked as answers. Use open-ended discussion format for Ideas, Show and Tell, and General.
3. Confirm the actual category slugs match the template filenames: q-a, ideas, show-and-tell, and general. If any slug differs, rename the corresponding template file to match it.
4. Keep Announcements for maintainer communications. A custom form is not needed for that category.
5. Merge the template files into the default branch, then open a new discussion in each category to verify that the intended fields render correctly. Forms in a feature branch do not become active before they are merged.
6. Review the category descriptions and welcome/introductory content in the GitHub UI as needed. Category settings are not controlled by these YAML files.

Discussion forms do not support polls. These files intentionally do not set automatic labels, because labels must already exist on the repository for them to be applied reliably.

Reference: [GitHub Docs - Creating discussion category forms](https://docs.github.com/en/discussions/managing-discussions-for-your-community/creating-discussion-category-forms).
