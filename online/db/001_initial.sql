CREATE TABLE IF NOT EXISTS schema_version (
    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    version integer NOT NULL,
    key_fingerprint text NOT NULL
);
CREATE TABLE guests (
    id uuid PRIMARY KEY,
    token_hash text NOT NULL UNIQUE,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL
);
CREATE TABLE matches (
    id uuid PRIMARY KEY,
    version bigint NOT NULL CHECK(version >= 0),
    state jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE memberships (
    match_id uuid NOT NULL REFERENCES matches(id),
    guest_id uuid NOT NULL REFERENCES guests(id),
    seat integer NOT NULL CHECK (seat IN (0, 1)),
    PRIMARY KEY(match_id, guest_id),
    UNIQUE(match_id, seat)
);
CREATE TABLE invitations (
    token_hash text PRIMARY KEY,
    match_id uuid NOT NULL UNIQUE REFERENCES matches(id),
    expires_at timestamptz NOT NULL,
    consumed_by uuid REFERENCES guests(id),
    consumed_at timestamptz
);
CREATE TABLE guest_receipts (
    guest_id uuid NOT NULL REFERENCES guests(id),
    command_id text NOT NULL,
    fingerprint text NOT NULL,
    kind text NOT NULL CHECK(kind IN ('create', 'join')),
    result jsonb NOT NULL,
    PRIMARY KEY(guest_id, command_id)
);
CREATE TABLE command_receipts (
    match_id uuid NOT NULL REFERENCES matches(id),
    command_id text NOT NULL,
    guest_id uuid NOT NULL REFERENCES guests(id),
    fingerprint text NOT NULL,
    http_status integer NOT NULL,
    receipt jsonb NOT NULL,
    PRIMARY KEY(match_id, command_id)
);
CREATE TABLE match_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    match_id uuid NOT NULL REFERENCES matches(id),
    version bigint NOT NULL,
    kind text NOT NULL,
    action_id text,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(match_id, version)
);
CREATE FUNCTION reject_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'History is append-only';
END;
$$;
CREATE TRIGGER events_append_only BEFORE UPDATE OR DELETE ON match_events
    FOR EACH ROW EXECUTE FUNCTION reject_history_mutation();
CREATE TRIGGER receipts_append_only BEFORE UPDATE OR DELETE ON command_receipts
    FOR EACH ROW EXECUTE FUNCTION reject_history_mutation();
CREATE TRIGGER guest_receipts_append_only BEFORE UPDATE OR DELETE ON guest_receipts
    FOR EACH ROW EXECUTE FUNCTION reject_history_mutation();
