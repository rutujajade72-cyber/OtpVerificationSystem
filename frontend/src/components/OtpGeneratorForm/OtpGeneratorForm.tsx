import { useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { MAX_USER_ID_LENGTH } from '../../config';
import type { GenerateOtpRequest } from '../../types/otp';
import { dateTimeLocalToIso, toDateTimeLocalValue } from '../../utils/dateTime';

interface OtpGeneratorFormProps {
  onGenerate: (request: GenerateOtpRequest) => void | Promise<void>;
  isGenerating: boolean;
  /** An error returned by the API, shown under the form. */
  error: string | null;
}

export function OtpGeneratorForm({ onGenerate, isGenerating, error }: OtpGeneratorFormProps) {
  const id = useId();
  const [userId, setUserId] = useState('');
  const [dateTime, setDateTime] = useState(() => toDateTimeLocalValue(new Date()));
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleUserIdChange = (event: ChangeEvent<HTMLInputElement>) => {
    setUserId(event.target.value);
    setValidationError(null);
  };

  const handleDateTimeChange = (event: ChangeEvent<HTMLInputElement>) => {
    setDateTime(event.target.value);
    setValidationError(null);
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedUserId = userId.trim();
    if (trimmedUserId === '') {
      setValidationError('User Id is required.');
      return;
    }

    const iso = dateTimeLocalToIso(dateTime);
    if (iso === null) {
      setValidationError('A valid date and time is required.');
      return;
    }

    setValidationError(null);
    void onGenerate({ userId: trimmedUserId, dateTime: iso });
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
        <label htmlFor={`${id}-date-time`}>Date and time</label>
        <div className="field__row">
          <input
            id={`${id}-date-time`}
            type="datetime-local"
            step={1}
            value={dateTime}
            onChange={handleDateTimeChange}
          />
          <button
            type="button"
            className="button button--secondary"
            onClick={() => setDateTime(toDateTimeLocalValue(new Date()))}
          >
            Now
          </button>
        </div>
      </div>

      <button type="submit" className="button button--primary" disabled={isGenerating}>
        {isGenerating ? 'Generating…' : 'Generate password'}
      </button>

      {message !== null && (
        <p className="message message--error" role="alert">
          {message}
        </p>
      )}
    </form>
  );
}
