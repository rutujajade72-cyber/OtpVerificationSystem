import { otpApi, type OtpApi } from './api/otpApi';
import { OtpDisplay } from './components/OtpDisplay/OtpDisplay';
import { OtpGeneratorForm } from './components/OtpGeneratorForm/OtpGeneratorForm';
import { OtpValidatorForm } from './components/OtpValidatorForm/OtpValidatorForm';
import { DEFAULT_VALIDITY_SECONDS, OTP_CODE_LENGTH } from './config';
import { useCountdown } from './hooks/useCountdown';
import { useOtp } from './hooks/useOtp';

interface AppProps {
  /** The API implementation. Tests pass a fake one. */
  api?: OtpApi;
}

export default function App({ api = otpApi }: AppProps) {
  const {
    otp,
    expiresAtMs,
    isGenerating,
    generateError,
    validation,
    isValidating,
    validateError,
    generate,
    validate,
  } = useOtp(api);
  const remainingMs = useCountdown(expiresAtMs);

  // A new password remounts the validator, which clears the typed code and pre-fills the user.
  const validatorKey = otp === null ? 'no-password' : `${otp.userId}-${otp.issuedAt}`;

  return (
    <main className="page">
      <header className="page__header">
        <h1>One-Time Password</h1>
        <p>
          Generate a password from a User Id and a date/time. Each password is valid for{' '}
          {DEFAULT_VALIDITY_SECONDS} seconds.
        </p>
      </header>

      <div className="layout">
        <section className="card" aria-labelledby="generate-heading">
          <h2 id="generate-heading">1. Generate</h2>
          <OtpGeneratorForm
            onGenerate={generate}
            isGenerating={isGenerating}
            error={generateError}
          />
          <OtpDisplay otp={otp} remainingMs={remainingMs} />
        </section>

        <section className="card" aria-labelledby="validate-heading">
          <h2 id="validate-heading">2. Validate</h2>
          <OtpValidatorForm
            key={validatorKey}
            codeLength={otp?.code.length ?? OTP_CODE_LENGTH}
            defaultUserId={otp?.userId ?? ''}
            onValidate={validate}
            isValidating={isValidating}
            result={validation}
            error={validateError}
          />
        </section>
      </div>
    </main>
  );
}
