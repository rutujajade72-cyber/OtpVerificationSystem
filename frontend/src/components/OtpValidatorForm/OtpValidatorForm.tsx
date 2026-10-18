import { useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { MAX_USER_ID_LENGTH } from '../../config';
import type { ValidateOtpRequest, ValidateOtpResponse } from '../../types/otp';

interface OtpValidatorFormProps {
  /** The number of digits a password has. The field accepts exactly this many. */
  codeLength: number;
  /** Pre-fills the User Id, typically with the user of the last generated password. */
  defaultUserId: string;
  onValidate: (request: ValidateOtpRequest) => void | Promise<void>;
  isValidating: boolean;
  result: ValidateOtpResponse | null;
  /** A transport or server error, shown instead of a result. */
  error: string | null;
}

export function OtpValidatorForm({
  codeLength,
  defaultUserId,
  onValidate,
  isValidating,
  result,
  error,
}: OtpValidatorFormProps) {
  const id = useId();
  const [userId, setUserId] = useState(defaultUserId);
  const [code, setCode] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleUserIdChange = (event: ChangeEvent<HTMLInputElement>) => {
    setUserId(event.target.value);
    setValidationError(null);
  };

  const handleCodeChange = (event: ChangeEvent<HTMLInputElement>) => {
    // Only digits can be typed or pasted into the field.
    setCode(event.target.value.replace(/\D/g, ''));
    setValidationError(null);
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedUserId = userId.trim();
    if (trimmedUserId === '') {
      setValidationError('User Id is required.');
      return;
    }

    if (code.length !== codeLength) {
      setValidationError(`Enter the ${codeLength} digit password.`);
      return;
    }

    setValidationError(null);
    void onValidate({ userId: trimmedUserId, code });
  };

  const message = validationError ?? error;

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      <div className="field">
        <label htmlFor={`${id}-user-id`}>User Id</label>
        <input
          id={`${id}-user-id`}
          type="text"
          value={userId}
          onChange={handleUserIdChange}
          maxLength={MAX_USER_ID_LENGTH}
          autoComplete="off"
          spellCheck={false}
        />
      </div>

      <div className="field">
        <label htmlFor={`${id}-code`}>One-time password</label>
        <input
          id={`${id}-code`}
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          value={code}
          onChange={handleCodeChange}
          maxLength={codeLength}
        />
      </div>

      <button type="submit" className="button button--primary" disabled={isValidating}>
        {isValidating ? 'Validating…' : 'Validate password'}
      </button>

      {message !== null && (
        <p className="message message--error" role="alert">
          {message}
        </p>
      )}

      {message === null && result !== null && (
        <p
          className={`message ${result.isValid ? 'message--success' : 'message--error'}`}
          role="status"
          data-testid="validation-result"
        >
          <strong>{result.isValid ? 'Valid.' : 'Not valid.'}</strong> {result.message}
        </p>
      )}
    </form>
  );
}
