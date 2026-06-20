Add a "Supplier" resource to this application's backend.

A supplier has:
- `Name` — text, required, at most 100 characters.
- `Email` — text, required, must be a valid email address.

Expose REST endpoints under `/api/suppliers` to create a supplier, get one by id, list all,
update one, and delete one. Persist suppliers in the application's database (add whatever entity,
mapping, and migration are needed). A create or update request with an invalid Name or Email must
be rejected with HTTP 400 and must not persist anything. The endpoints must require authentication.

Use the application's existing stack and conventions. Do not introduce a new web framework.
