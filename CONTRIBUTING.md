# Contributing

Thanks for taking the time. A few things worth knowing before you open a pull request.

## License

This project is licensed under **AGPL-3.0-or-later**. Every contribution you submit is
licensed under the same terms. If you are not comfortable with that, please do not submit
a contribution.

The reason for copyleft here is a promise, not a business model: this software is free and
stays free. AGPL is what makes that promise binding on everyone downstream, not just on
the author.

## Developer Certificate of Origin

Every commit must be signed off. This is a statement that you wrote the code, or otherwise
have the right to submit it under the project's license. It is not a copyright assignment
and you keep the copyright to your work.

Add the sign-off with `-s`:

```bash
git commit -s -m "your message"
```

That appends a line to the commit message:

```
Signed-off-by: Your Name <your.email@example.com>
```

The full text you are certifying is the Developer Certificate of Origin 1.1, reproduced in
[DCO](DCO). Commits without a sign-off will not be merged.

## Pull requests

- One concern per pull request. A license fix and a feature do not belong in the same
  branch.
- Match the surrounding code. Comment density, naming, and idiom should read as if the
  file had one author.
- Run the project's test suite before opening the pull request. If the README documents a
  command for it, use that one.

## Reporting problems

Open an issue with what you did, what you expected, and what happened instead. Steps to
reproduce are worth more than a description of the symptom.
