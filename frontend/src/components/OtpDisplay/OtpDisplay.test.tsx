import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { createGenerateResponse } from '../../test/fixtures';
import { OtpDisplay } from './OtpDisplay';

describe('OtpDisplay', () => {
  it('shows a placeholder before any password exists', () => {
    render(<OtpDisplay otp={null} remainingMs={0} />);

    expect(screen.getByText('Generate a password to see it here.')).toBeInTheDocument();
    expect(screen.queryByTestId('otp-code')).not.toBeInTheDocument();
  });

  it('shows the password and the user it belongs to', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={30_000} />);

    expect(screen.getByTestId('otp-code')).toHaveTextContent('482913');
    expect(screen.getByText('john.doe')).toBeInTheDocument();
    expect(screen.getByText(/One-time password for/)).toBeInTheDocument();
  });

  it('keeps the leading zeros of the code', () => {
    render(<OtpDisplay otp={createGenerateResponse({ code: '004213' })} remainingMs={30_000} />);

    expect(screen.getByTestId('otp-code')).toHaveTextContent('004213');
  });

  it('shows the remaining seconds', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={30_000} />);

    expect(screen.getByTestId('otp-timer')).toHaveTextContent('30 seconds remaining');
  });

  it('rounds partial seconds up so the display never shows 0 while time is left', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={29_001} />);

    expect(screen.getByTestId('otp-timer')).toHaveTextContent('30 seconds remaining');
  });

  it('uses the singular for one second', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={1_000} />);

    expect(screen.getByTestId('otp-timer')).toHaveTextContent('1 second remaining');
  });

  it('reflects the remaining time in an accessible progress bar', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={15_000} />);

    const progress = screen.getByRole('progressbar', { name: 'Time remaining' });
    expect(progress).toHaveAttribute('aria-valuenow', '15');
    expect(progress).toHaveAttribute('aria-valuemax', '30');
    expect(progress.firstElementChild).toHaveStyle({ width: '50%' });
  });

  it('is in the normal state while plenty of time is left', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={20_000} />);

    expect(screen.getByTestId('otp-display')).toHaveClass('otp--active');
  });

  it('switches to the warning state for the last ten seconds', () => {
    render(<OtpDisplay otp={createGenerateResponse()} remainingMs={10_000} />);

    expect(screen.getByTestId('otp-display')).toHaveClass('otp--ending');
    expect(screen.getByTestId('otp-display')).not.toHaveClass('otp--active');
  });

  describe('when the password has expired', () => {
    it('says Expired and explains what to do', () => {
      render(<OtpDisplay otp={createGenerateResponse()} remainingMs={0} />);

      expect(screen.getByText('Expired')).toBeInTheDocument();
      expect(
        screen.getByText('This password is no longer valid. Generate a new one to continue.'),
      ).toBeInTheDocument();
    });

    it('stops showing the countdown but keeps the struck-through code visible', () => {
      render(<OtpDisplay otp={createGenerateResponse()} remainingMs={0} />);

      expect(screen.queryByTestId('otp-timer')).not.toBeInTheDocument();
      expect(screen.getByTestId('otp-code')).toHaveTextContent('482913');
      expect(screen.getByTestId('otp-display')).toHaveClass('otp--expired');
    });

    it('announces the expiry to screen readers', () => {
      render(<OtpDisplay otp={createGenerateResponse()} remainingMs={0} />);

      expect(screen.getByText('The one-time password has expired.')).toBeInTheDocument();
    });

    it('does not announce anything while the password is still valid', () => {
      render(<OtpDisplay otp={createGenerateResponse()} remainingMs={5_000} />);

      expect(screen.queryByText('The one-time password has expired.')).not.toBeInTheDocument();
    });
  });
});
