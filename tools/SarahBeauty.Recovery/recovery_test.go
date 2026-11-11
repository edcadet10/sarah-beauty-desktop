package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

const fictionalPhrase = "fictional meadow lantern window"

func call(t *testing.T, command request) response {
	t.Helper()
	data, err := json.Marshal(command)
	if err != nil {
		t.Fatal("Could not encode a fictional command")
	}
	result, err := serve(bytes.NewReader(data))
	if err != nil {
		t.Fatalf("Operation %s failed: %s", command.Operation, safeCode(err))
	}
	return result
}

func sourceFile(t *testing.T, name string, content []byte) string {
	t.Helper()
	path := filepath.Join(t.TempDir(), name)
	if os.WriteFile(path, content, 0600) != nil {
		t.Fatal("Could not write the fictional source")
	}
	return path
}

func readFile(t *testing.T, path string) []byte {
	t.Helper()
	content, err := os.ReadFile(path)
	if err != nil {
		t.Fatal("Expected output was not available")
	}
	return content
}

func TestProtectedRecoveryAndStreamingRoundTrip(t *testing.T) {
	bundle := call(t, request{Version: 1, Operation: "create-recovery", Passphrase: fictionalPhrase})
	if bytes.Contains(bundle.ProtectedIdentity, []byte(fictionalPhrase)) {
		t.Fatal("Recovery file contains the passphrase")
	}
	inspected := call(t, request{Version: 1, Operation: "inspect-recovery", Passphrase: fictionalPhrase, ProtectedIdentity: bundle.ProtectedIdentity})
	if inspected.Recipient != bundle.Recipient {
		t.Fatal("Protected recovery does not retain its recipient")
	}
	public := call(t, request{Version: 1, Operation: "inspect-recipient", Recipient: bundle.Recipient})
	if public.Recipient != bundle.Recipient {
		t.Fatal("Public recipient inspection changed the encryption destination")
	}
	if _, err := serve(strings.NewReader(`{"version":1,"operation":"inspect-recipient","recipient":"invalid"}`)); !errors.Is(err, errRecipient) {
		t.Fatal("Invalid public recipient was accepted")
	}
	for _, size := range []int{0, 65536, 150000} {
		plain := bytes.Repeat([]byte{31}, size)
		input := sourceFile(t, "fictional.bin", plain)
		encrypted := filepath.Join(t.TempDir(), "fictional.age")
		restored := filepath.Join(t.TempDir(), "restored.bin")
		result := call(t, request{Version: 1, Operation: "encrypt", Recipient: bundle.Recipient, InputPath: input, OutputPath: encrypted})
		if result.Bytes <= int64(size) || len(result.SHA256) != 64 {
			t.Fatal("Encrypted file metadata is incomplete")
		}
		ciphertext := readFile(t, encrypted)
		digest := sha256.Sum256(ciphertext)
		if result.Bytes != int64(len(ciphertext)) || result.SHA256 != hex.EncodeToString(digest[:]) {
			t.Fatal("Encrypted output hash/size does not match the actual file")
		}
		call(t, request{Version: 1, Operation: "decrypt", Passphrase: fictionalPhrase, ProtectedIdentity: bundle.ProtectedIdentity, InputPath: encrypted, OutputPath: restored})
		if !bytes.Equal(plain, readFile(t, restored)) {
			t.Fatal("Streaming output differs from the original")
		}
	}
}

func TestWrongRecoveryAndAlteredCiphertextLeaveNoOutput(t *testing.T) {
	bundle := call(t, request{Version: 1, Operation: "create-recovery", Passphrase: fictionalPhrase})
	other := call(t, request{Version: 1, Operation: "create-recovery", Passphrase: fictionalPhrase})
	input := sourceFile(t, "fictional.bin", bytes.Repeat([]byte("Fictional archive data\n"), 9000))
	encrypted := filepath.Join(t.TempDir(), "source.age")
	call(t, request{Version: 1, Operation: "encrypt", Recipient: bundle.Recipient, InputPath: input, OutputPath: encrypted})
	base := request{Version: 1, Operation: "decrypt", Passphrase: fictionalPhrase, ProtectedIdentity: bundle.ProtectedIdentity, InputPath: encrypted}
	wrongPhrase := base
	wrongPhrase.Passphrase = "fictional different recovery phrase"
	wrongKey := base
	wrongKey.ProtectedIdentity = other.ProtectedIdentity
	corrupted := readFile(t, encrypted)
	corrupted[len(corrupted)-1] ^= 1
	changed := base
	changed.InputPath = sourceFile(t, "changed.age", corrupted)
	truncated := base
	original := readFile(t, encrypted)
	truncated.InputPath = sourceFile(t, "truncated.age", original[:len(original)-1])
	for _, command := range []request{wrongPhrase, wrongKey, changed, truncated} {
		command.OutputPath = filepath.Join(t.TempDir(), "must-not-publish.bin")
		_, err := decryptArchive(command)
		if !errors.Is(err, errAuthentication) {
			t.Fatalf("Expected authenticated rejection; received %s", safeCode(err))
		}
		if _, err := os.Stat(command.OutputPath); !os.IsNotExist(err) {
			t.Fatal("Failed decryption retained a plaintext output")
		}
	}
	existing := sourceFile(t, "existing.bin", []byte("Fictional original stays here"))
	base.OutputPath = existing
	if _, err := decryptArchive(base); !errors.Is(err, errOutput) {
		t.Fatal("An existing output was not protected")
	}
	if string(readFile(t, existing)) != "Fictional original stays here" {
		t.Fatal("The existing output was modified")
	}
}

func TestReferenceCLIInteroperability(t *testing.T) {
	cli := os.Getenv("SARAHBEAUTY_AGE_CLI")
	if cli == "" {
		t.Fatal("Set SARAHBEAUTY_AGE_CLI to the pinned age 1.3.2 reference executable")
	}
	version, err := exec.Command(cli, "--version").Output()
	if err != nil || strings.TrimSpace(string(version)) != "v1.3.2" {
		t.Fatal("The pinned reference CLI was not available")
	}
	bundle := call(t, request{Version: 1, Operation: "create-recovery", Passphrase: fictionalPhrase})
	identity, err := unlockRecovery(bundle.ProtectedIdentity, fictionalPhrase)
	if err != nil {
		t.Fatal("Fictional recovery could not be unlocked")
	}
	// This plaintext identity exists only in the temporary interoperability fixture, never in the app's production protocol.
	key := sourceFile(t, "fictional-reference-identity.txt", []byte(identity.String()+"\n"))
	plain := bytes.Repeat([]byte("Fictional interoperability payload\n"), 5000)
	input := sourceFile(t, "source.bin", plain)
	encrypted := filepath.Join(t.TempDir(), "helper.age")
	restored := filepath.Join(t.TempDir(), "reference-restored.bin")
	call(t, request{Version: 1, Operation: "encrypt", Recipient: bundle.Recipient, InputPath: input, OutputPath: encrypted})
	if exec.Command(cli, "--decrypt", "-i", key, "-o", restored, encrypted).Run() != nil {
		t.Fatal("Reference CLI rejected the helper archive")
	}
	if !bytes.Equal(plain, readFile(t, restored)) {
		t.Fatal("Reference CLI restored different data")
	}
	reference := filepath.Join(t.TempDir(), "reference.age")
	helperRestored := filepath.Join(t.TempDir(), "helper-restored.bin")
	if exec.Command(cli, "-r", bundle.Recipient, "-o", reference, input).Run() != nil {
		t.Fatal("Reference CLI could not encrypt the fixture")
	}
	call(t, request{Version: 1, Operation: "decrypt", Passphrase: fictionalPhrase, ProtectedIdentity: bundle.ProtectedIdentity, InputPath: reference, OutputPath: helperRestored})
	if !bytes.Equal(plain, readFile(t, helperRestored)) {
		t.Fatal("Helper rejected reference CLI data")
	}
}

func TestProtocolBoundsAndRecoveryWorkLimit(t *testing.T) {
	for _, input := range []string{`{"version":2,"operation":"create-recovery"}`, `{"version":1,"operation":"unknown"}`, `{"version":1,"operation":"create-recovery","unexpected":"fictional secret"}`, `{} {}`, strings.Repeat("x", 128*1024+1)} {
		_, err := serve(strings.NewReader(input))
		if !errors.Is(err, errRequest) || strings.Contains(safeCode(err), "secret") {
			t.Fatal("Invalid protocol did not return a fixed safe code")
		}
	}
	bundle := call(t, request{Version: 1, Operation: "create-recovery", Passphrase: fictionalPhrase})
	excessive := bytes.Replace(bundle.ProtectedIdentity, []byte(" 18\n"), []byte(" 30\n"), 1)
	if bytes.Equal(excessive, bundle.ProtectedIdentity) {
		t.Fatal("The recovery work-factor probe did not change its header")
	}
	if _, err := unlockRecovery(excessive, fictionalPhrase); !errors.Is(err, errAuthentication) {
		t.Fatal("Excessive recovery work was not rejected")
	}
	var output bytes.Buffer
	if !errors.Is(copyBounded(&output, strings.NewReader(strings.Repeat("x", 33)), 32), errLimit) {
		t.Fatal("The streaming bound was not enforced")
	}
	if safeCode(errors.New(fictionalPhrase)) != "OperationFailed" {
		t.Fatal("Unexpected errors must not echo private inputs")
	}
}
