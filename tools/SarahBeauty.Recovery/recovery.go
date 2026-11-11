package main

import (
	"bytes"
	"filippo.io/age"
	"io"
	"unicode/utf8"
)

func validPassphrase(passphrase string) bool {
	count := utf8.RuneCountInString(passphrase)
	return count >= 15 && count <= 256
}

func createRecovery(passphrase string) (response, error) {
	if !validPassphrase(passphrase) {
		return response{}, errPassphrase
	}
	identity, err := age.GenerateHybridIdentity()
	if err != nil {
		return response{}, err
	}
	recipient, err := age.NewScryptRecipient(passphrase)
	if err != nil {
		return response{}, errPassphrase
	}
	// age 1.3.2's default: scrypt N=2^18, r=8, p=1. Keep the recovery policy explicit.
	recipient.SetWorkFactor(18)
	var sealed bytes.Buffer
	writer, err := age.Encrypt(&sealed, recipient)
	if err != nil {
		return response{}, err
	}
	if _, err = io.WriteString(writer, identity.String()+"\n"); err != nil {
		return response{}, err
	}
	if err = writer.Close(); err != nil {
		return response{}, err
	}
	return response{OK: true, Recipient: identity.Recipient().String(), ProtectedIdentity: sealed.Bytes()}, nil
}

func unlockRecovery(sealed []byte, passphrase string) (*age.HybridIdentity, error) {
	if !validPassphrase(passphrase) {
		return nil, errPassphrase
	}
	if len(sealed) == 0 || len(sealed) > 64*1024 {
		return nil, errRequest
	}
	key, err := age.NewScryptIdentity(passphrase)
	if err != nil {
		return nil, errAuthentication
	}
	key.SetMaxWorkFactor(18)
	reader, err := age.Decrypt(bytes.NewReader(sealed), key)
	if err != nil {
		return nil, errAuthentication
	}
	plain, err := io.ReadAll(io.LimitReader(reader, 16*1024+1))
	defer clear(plain)
	if err != nil || len(plain) > 16*1024 {
		return nil, errAuthentication
	}
	identity, err := age.ParseHybridIdentity(string(bytes.TrimSpace(plain)))
	if err != nil {
		return nil, errAuthentication
	}
	return identity, nil
}
