package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"io"
	"os"
)

type request struct {
	Version           int    `json:"version"`
	Operation         string `json:"operation"`
	Passphrase        string `json:"passphrase,omitempty"`
	ProtectedIdentity []byte `json:"protectedIdentity,omitempty"`
	Recipient         string `json:"recipient,omitempty"`
	InputPath         string `json:"inputPath,omitempty"`
	OutputPath        string `json:"outputPath,omitempty"`
}

type response struct {
	OK                bool   `json:"ok"`
	Code              string `json:"code,omitempty"`
	Recipient         string `json:"recipient,omitempty"`
	ProtectedIdentity []byte `json:"protectedIdentity,omitempty"`
	SHA256            string `json:"sha256,omitempty"`
	Bytes             int64  `json:"bytes,omitempty"`
}

func main() {
	if len(os.Args) != 1 {
		_ = json.NewEncoder(os.Stdout).Encode(response{Code: "InvalidRequest"})
		os.Exit(1)
	}
	result, err := serve(os.Stdin)
	if err != nil {
		result = response{Code: safeCode(err)}
	}
	_ = json.NewEncoder(os.Stdout).Encode(result)
	if err != nil {
		os.Exit(1)
	}
}

func serve(input io.Reader) (response, error) {
	data, err := io.ReadAll(io.LimitReader(input, 128*1024+1))
	if err != nil || len(data) > 128*1024 {
		return response{}, errRequest
	}
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.DisallowUnknownFields()
	var command request
	if decoder.Decode(&command) != nil {
		return response{}, errRequest
	}
	var trailing any
	if decoder.Decode(&trailing) != io.EOF || command.Version != 1 {
		return response{}, errRequest
	}
	switch command.Operation {
	case "create-recovery":
		return createRecovery(command.Passphrase)
	case "inspect-recovery":
		identity, err := unlockRecovery(command.ProtectedIdentity, command.Passphrase)
		if err != nil {
			return response{}, err
		}
		return response{OK: true, Recipient: identity.Recipient().String()}, nil
	case "encrypt":
		return encryptArchive(command)
	case "decrypt":
		return decryptArchive(command)
	default:
		return response{}, errRequest
	}
}

var (
	errRequest        = errors.New("InvalidRequest")
	errPassphrase     = errors.New("InvalidPassphrase")
	errAuthentication = errors.New("AuthenticationFailed")
	errRecipient      = errors.New("UnsupportedRecipient")
	errInput          = errors.New("InputUnavailable")
	errOutput         = errors.New("OutputUnavailable")
	errLimit          = errors.New("SizeLimitExceeded")
)

func safeCode(err error) string {
	for _, known := range []error{errRequest, errPassphrase, errAuthentication, errRecipient, errInput, errOutput, errLimit} {
		if errors.Is(err, known) {
			return known.Error()
		}
	}
	return "OperationFailed"
}
