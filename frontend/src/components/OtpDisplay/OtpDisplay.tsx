import { ENDING_SOON_SECONDS } from '../../config';
import type { GenerateOtpResponse } from '../../types/otp';
import { msToWholeSeconds } from '../../utils/dateTime';

interface OtpDisplayProps {
  otp: GenerateOtpResponse | null;
  /** Milliseconds left, from useCountdown. Zero means the password has expired. */
  remainingMs: number;
}

export function OtpDisplay({ otp, remainingMs }: OtpDisplayProps) {
  if (otp === null) {
    return <p className="otp-placeholder">Generate a password to see it here.</p>;
  }

  const remainingSeconds = msToWholeSeconds(remainingMs);
  const isExpired = remainingMs <= 0;
  const isEndingSoon = !isExpired && remainingSeconds <= ENDING_SOON_SECONDS;
  const progress = Math.min(1, remainingMs / (otp.validitySeconds * 1000));

  const stateClass = isExpired ? 'otp--expired' : isEndingSoon ? 'otp--ending' : 'otp--active';

  return (
    <div className={`otp ${stateClass}`} data-testid="otp-display">
      <p className="otp__label">
        One-time password for <strong className="otp__user">{otp.userId}</strong>
      </p>

      <p className="otp__code" data-testid="otp-code">
        {otp.code}
      </p>

      <div
        className="otp__progress"
        role="progressbar"
        aria-label="Time remaining"
        aria-valuemin={0}
        aria-valuemax={otp.validitySeconds}
        aria-valuenow={remainingSeconds}
      >
        <div className="otp__progress-fill" style={{ width: `${progress * 100}%` }} />
      </div>

      {isExpired ? (
        <>
          <p className="otp__status otp__status--expired">Expired</p>
          <p className="otp__hint">
            This password is no longer valid. Generate a new one to continue.
          </p>
        </>
      ) : (
        <p className="otp__status" role="timer" data-testid="otp-timer">
          {remainingSeconds} {remainingSeconds === 1 ? 'second' : 'seconds'} remaining
        </p>
      )}

      {/* Screen readers hear the expiry once, not every tick of the countdown. */}
      <p className="visually-hidden" aria-live="polite">
        {isExpired ? 'The one-time password has expired.' : ''}
      </p>
    </div>
  );
}
