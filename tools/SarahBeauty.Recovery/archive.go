package main

import (
	"crypto/sha256"
	"encoding/hex"
	"filippo.io/age"
	"io"
	"os"
)

const maximumFileBytes int64 = 64 << 30

// Include the format's bounded header, nonce, and authentication tags when accepting ciphertext.
const maximumCipherBytes = maximumFileBytes + (maximumFileBytes/(64*1024)+1)*16 + 2*1024*1024 + 32

func encryptArchive(command request) (response, error) {
	recipient, err := age.ParseHybridRecipient(command.Recipient)
	if err != nil {
		return response{}, errRecipient
	}
	return transformFile(command.InputPath, command.OutputPath, maximumFileBytes, func(output io.Writer, input io.Reader) error {
		writer, err := age.Encrypt(output, recipient)
		if err != nil {
			return err
		}
		if err = copyBounded(writer, input, maximumFileBytes); err != nil {
			return err
		}
		return writer.Close()
	})
}

func decryptArchive(command request) (response, error) {
	identity, err := unlockRecovery(command.ProtectedIdentity, command.Passphrase)
	if err != nil {
		return response{}, err
	}
	return transformFile(command.InputPath, command.OutputPath, maximumCipherBytes, func(output io.Writer, input io.Reader) error {
		reader, err := age.Decrypt(input, identity)
		if err != nil {
			return errAuthentication
		}
		if err = copyBounded(output, reader, maximumFileBytes); err != nil {
			return errAuthentication
		}
		return nil
	})
}

func copyBounded(output io.Writer, input io.Reader, maximum int64) error {
	count, err := io.Copy(output, io.LimitReader(input, maximum+1))
	if err != nil {
		return err
	}
	if count > maximum {
		return errLimit
	}
	return nil
}

func transformFile(inputPath, outputPath string, maximumInput int64, transform func(io.Writer, io.Reader) error) (result response, err error) {
	input, err := os.Open(inputPath)
	if err != nil {
		return response{}, errInput
	}
	defer input.Close()
	info, err := input.Stat()
	if err != nil || !info.Mode().IsRegular() {
		return response{}, errInput
	}
	if info.Size() > maximumInput {
		return response{}, errLimit
	}
	output, err := os.OpenFile(outputPath, os.O_WRONLY|os.O_CREATE|os.O_EXCL, 0600)
	if err != nil {
		return response{}, errOutput
	}
	complete := false
	defer func() {
		_ = output.Close()
		if !complete {
			_ = os.Remove(outputPath)
		}
	}()
	hash := sha256.New()
	if err = transform(io.MultiWriter(output, hash), input); err != nil {
		return response{}, err
	}
	if output.Sync() != nil || output.Close() != nil {
		return response{}, errOutput
	}
	stored, err := os.Stat(outputPath)
	if err != nil {
		return response{}, errOutput
	}
	complete = true
	return response{OK: true, SHA256: hex.EncodeToString(hash.Sum(nil)), Bytes: stored.Size()}, nil
}
