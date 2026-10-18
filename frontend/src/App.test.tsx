import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import App from './App';
import { ApiError } from './api/ApiError';
import { createFakeApi, createGenerateResponse, createValidateResponse } from './test/fixtures';

const ISO_UTC = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/;

function generateSection() {
  return within(screen.getByRole('region', { name: '1. Generate' }));
}

function validateSection() {
  return within(screen.getByRole('region', { name: '2. Validate' }));
}

async function generatePassword(user: ReturnType<typeof userEvent.setup>, userId = 'john.doe') {
  await user.type(generateSection().getByLabelText('User Id'), userId);
  await user.click(generateSection().getByRole('button', { name: 'Generate password' }));
}

async function validateCode(user: ReturnType<typeof userEvent.setup>, code = '482913') {
  await user.type(validateSection().getByLabelText('One-time password'), code);
  await user.click(validateSection().getByRole('button', { name: 'Validate password' }));
}

describe('App', () => {
  it('renders the title, the explanation and both steps', () => {
    const { api } = createFakeApi();

    render(<App api={api} />);

    expect(
      screen.getByRole('heading', { level: 1, name: 'One-Time Password' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Each password is valid for 30 seconds\./)).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: '1. Generate' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: '2. Validate' })).toBeInTheDocument();
    expect(screen.getByText('Generate a password to see it here.')).toBeInTheDocument();
  });

  describe('generating a password', () => {
    it('sends the user id and an ISO date to the API and shows the password', async () => {
      const { api, generate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse());
      const user = userEvent.setup();
      render(<App api={api} />);

      await generatePassword(user);

      expect(await screen.findByTestId('otp-code')).toHaveTextContent('482913');
      expect(generate).toHaveBeenCalledWith({
        userId: 'john.doe',
        dateTime: expect.stringMatching(ISO_UTC),
      });
      expect(screen.getByTestId('otp-timer')).toHaveTextContent('30 seconds remaining');
    });

    it('pre-fills the validator with the user of the new password', async () => {
      const { api, generate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse({ userId: 'jane.doe' }));
      const user = userEvent.setup();
      render(<App api={api} />);

      await generatePassword(user, 'jane.doe');
      await screen.findByTestId('otp-code');

      expect(validateSection().getByLabelText('User Id')).toHaveValue('jane.doe');
    });

    it('shows an error from the API and no password', async () => {
      const { api, generate } = createFakeApi();
      generate.mockRejectedValue(
        new ApiError('Too many incorrect attempts. Please try again in 42 second(s).', 429),
      );
      const user = userEvent.setup();
      render(<App api={api} />);

      await generatePassword(user);

      expect(await generateSection().findByRole('alert')).toHaveTextContent(
        'Too many incorrect attempts. Please try again in 42 second(s).',
      );
      expect(screen.queryByTestId('otp-code')).not.toBeInTheDocument();
    });

    it('does not call the API when the user id is missing', async () => {
      const { api, generate } = createFakeApi();
      const user = userEvent.setup();
      render(<App api={api} />);

      await user.click(generateSection().getByRole('button', { name: 'Generate password' }));

      expect(generateSection().getByRole('alert')).toHaveTextContent('User Id is required.');
      expect(generate).not.toHaveBeenCalled();
    });
  });

  describe('validating a password', () => {
    it('sends the user id and code to the API and shows a valid result', async () => {
      const { api, generate, validate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse());
      validate.mockResolvedValue(createValidateResponse());
      const user = userEvent.setup();
      render(<App api={api} />);
      await generatePassword(user);
      await screen.findByTestId('otp-code');

      await validateCode(user);

      expect(validate).toHaveBeenCalledWith({ userId: 'john.doe', code: '482913' });
      expect(await screen.findByTestId('validation-result')).toHaveTextContent(
        'Valid. The one-time password is valid.',
      );
    });

    it('explains why a password is not valid', async () => {
      const { api, validate } = createFakeApi();
      validate.mockResolvedValue(
        createValidateResponse({
          isValid: false,
          status: 'Invalid',
          message: 'The one-time password is incorrect. 2 attempt(s) remaining.',
          remainingAttempts: 2,
        }),
      );
      const user = userEvent.setup();
      render(<App api={api} />);
      await user.type(validateSection().getByLabelText('User Id'), 'john.doe');

      await validateCode(user, '000000');

      expect(await screen.findByTestId('validation-result')).toHaveTextContent(
        'Not valid. The one-time password is incorrect. 2 attempt(s) remaining.',
      );
    });

    it('shows a transport error from the API', async () => {
      const { api, validate } = createFakeApi();
      validate.mockRejectedValue(new ApiError('Unable to reach the OTP service.', 0));
      const user = userEvent.setup();
      render(<App api={api} />);
      await user.type(validateSection().getByLabelText('User Id'), 'john.doe');

      await validateCode(user);

      expect(await validateSection().findByRole('alert')).toHaveTextContent(
        'Unable to reach the OTP service.',
      );
    });

    it('accepts exactly as many digits as the generated password has', async () => {
      const { api, generate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse());
      const user = userEvent.setup();
      render(<App api={api} />);
      const codeField = () => validateSection().getByLabelText('One-time password');
      expect(codeField()).toHaveAttribute('maxlength', '6');

      await generatePassword(user);
      await screen.findByTestId('otp-code');
      await user.type(codeField(), '12345678');

      expect(codeField()).toHaveAttribute('maxlength', '6');
      expect(codeField()).toHaveValue('123456');
    });

    it('follows the length of the password the server generated', async () => {
      const { api, generate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse({ code: '12345678' }));
      const user = userEvent.setup();
      render(<App api={api} />);

      await generatePassword(user);
      await screen.findByTestId('otp-code');

      expect(validateSection().getByLabelText('One-time password')).toHaveAttribute(
        'maxlength',
        '8',
      );
    });

    it('forgets the previous result when a new password is generated', async () => {
      const { api, generate, validate } = createFakeApi();
      generate.mockResolvedValue(createGenerateResponse());
      validate.mockResolvedValue(createValidateResponse({ isValid: false, status: 'Invalid' }));
      const user = userEvent.setup();
      render(<App api={api} />);
      await generatePassword(user);
      await screen.findByTestId('otp-code');
      await validateCode(user, '000000');
      expect(await screen.findByTestId('validation-result')).toBeInTheDocument();

      generate.mockResolvedValue(createGenerateResponse({ issuedAt: '2026-10-17T10:05:00+00:00' }));
      await user.click(generateSection().getByRole('button', { name: 'Generate password' }));

      await screen.findByTestId('otp-code');
      expect(screen.queryByTestId('validation-result')).not.toBeInTheDocument();
      expect(validateSection().getByLabelText('One-time password')).toHaveValue('');
    });
  });

  describe('the 30-second countdown', () => {
    // Clicks use fireEvent rather than userEvent: user-event waits on timers between its actions,
    // and those waits never finish while the fake clock is paused.
    async function clickAndSettle(element: HTMLElement) {
      await act(async () => {
        fireEvent.click(element);
        await vi.advanceTimersByTimeAsync(0);
      });
    }

    async function startWithFakeClock(response = createGenerateResponse()) {
      vi.useFakeTimers();
      vi.setSystemTime(new Date(2026, 9, 17, 10, 0, 0));
      const fake = createFakeApi();
      fake.generate.mockResolvedValue(response);
      render(<App api={fake.api} />);

      fireEvent.change(generateSection().getByLabelText('User Id'), {
        target: { value: 'john.doe' },
      });
      await clickAndSettle(generateSection().getByRole('button', { name: 'Generate password' }));

      return fake;
    }

    function advance(milliseconds: number) {
      act(() => {
        vi.advanceTimersByTime(milliseconds);
      });
    }

    it('starts at the full validity and counts down every second', async () => {
      await startWithFakeClock();
      expect(screen.getByTestId('otp-timer')).toHaveTextContent('30 seconds remaining');

      advance(1_000);
      expect(screen.getByTestId('otp-timer')).toHaveTextContent('29 seconds remaining');

      advance(9_000);
      expect(screen.getByTestId('otp-timer')).toHaveTextContent('20 seconds remaining');
      expect(screen.getByRole('progressbar', { name: 'Time remaining' })).toHaveAttribute(
        'aria-valuenow',
        '20',
      );
    });

    it('warns during the last ten seconds', async () => {
      await startWithFakeClock();

      advance(25_000);

      expect(screen.getByTestId('otp-timer')).toHaveTextContent('5 seconds remaining');
      expect(screen.getByTestId('otp-display')).toHaveClass('otp--ending');
    });

    it('marks the password as expired after 30 seconds', async () => {
      await startWithFakeClock();

      advance(29_999);
      expect(screen.getByTestId('otp-timer')).toHaveTextContent('1 second remaining');
      expect(screen.queryByText('Expired')).not.toBeInTheDocument();

      advance(1);
      expect(screen.getByText('Expired')).toBeInTheDocument();
      expect(
        screen.getByText('This password is no longer valid. Generate a new one to continue.'),
      ).toBeInTheDocument();
      expect(screen.queryByTestId('otp-timer')).not.toBeInTheDocument();
      expect(screen.getByTestId('otp-code')).toHaveTextContent('482913');
      expect(screen.getByTestId('otp-display')).toHaveClass('otp--expired');
    });

    it('stays expired as time keeps passing', async () => {
      await startWithFakeClock();

      advance(120_000);

      expect(screen.getByText('Expired')).toBeInTheDocument();
    });

    it('starts a fresh countdown when a new password is generated', async () => {
      const { generate } = await startWithFakeClock();
      advance(31_000);
      expect(screen.getByText('Expired')).toBeInTheDocument();
      generate.mockResolvedValue(createGenerateResponse({ code: '111111' }));

      await clickAndSettle(generateSection().getByRole('button', { name: 'Generate password' }));

      expect(screen.getByTestId('otp-code')).toHaveTextContent('111111');
      expect(screen.getByTestId('otp-timer')).toHaveTextContent('30 seconds remaining');
      expect(screen.queryByText('Expired')).not.toBeInTheDocument();
    });

    it('counts from the remaining time the server reports, not from the clocks', async () => {
      await startWithFakeClock(createGenerateResponse({ remainingSeconds: 12 }));

      expect(screen.getByTestId('otp-timer')).toHaveTextContent('12 seconds remaining');
    });
  });
});
