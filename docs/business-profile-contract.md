# Prepare private business setup facts

The profile contract records a display/legal name, optional contact information and structured address, private EIN, explicitly selected USD currency and time zone, optional books/reporting start dates, and a separate new-business-only acknowledgment. Formation, EIN, banking and tax reviews each retain an explicit state, private reason and optional managed-file descriptor.

Unknown legal identity, EIN, currency, time zone, start dates and review states stay blank or Unconfirmed. A non-Unconfirmed review requires a private reason. A confirmed EIN needs a value; NotApplicable cannot retain one. EIN checks validate only the entered format. An owner review does not certify legal formation, registration, bank ownership or tax treatment.

The contract accepts only USD books when currency is explicitly selected. Contact validation shares the existing customer rules. Text is bounded and rejects unsupported control characters or malformed Unicode. Fourteen profile cases and six existing customer cases pass. This step is a pure contract: it creates no store/profile, verifies no physical evidence, grants no LIVE authority and posts no opening entry. Authenticated revisioned storage, evidence verification, UI and isolated LIVE creation follow separately.
