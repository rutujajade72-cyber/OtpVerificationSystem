import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { createValidateResponse } from '../../test/fixtures';
import { OtpValidatorForm } from './OtpValidatorForm';

function renderForm(props: Partial<ComponentProps<typeof OtpValidatorForm>> = {}) {
  const onValidate = vi.fn();
  render(
    <OtpValidatorForm
      codeLength={6}
      defaultUserId="john.doe"
      onValidate={onValidate}
      isValidating={false}
      result={null}
      error={null}
      {...props}
    />,
  );
  return { onValidate };
}

describe('OtpValidatorForm', () => {
  it('renders the labelled fields and the submit button', () => {
    renderForm();

    expect(screen.getByLabelText('User Id')).toBeInTheDocument();
    expect(screen.getByLabelText('One-time password')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Validate password' })).toBeInTheDocument();
  });

  it('pre-fills the user id', () => {
    renderForm({ defaultUserId: 'jane.doe' });

    expect(screen.getByLabelText('User Id')).toHaveValue('jane.doe');
  });

  it('accepts digits only in the password field', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText('One-time password'), 'a1b2c3');

    expect(screen.getByLabelText('One-time password')).toHaveValue('123');
  });

  it('limits the password to the code length', () => {
    renderForm();

    expect(screen.getByLabelText('One-time password')).toHaveAttribute('maxlength', '6');
  });

  it('ignores digits typed beyond the code length', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText('One-time password'), '1234567');

    expect(screen.getByLabelText('One-time password')).toHaveValue('123456');
  });

  it('follows a different code length', async () => {
    const user = userEvent.setup();
    const { onValidate } = renderForm({ codeLength: 8 });
    expect(screen.getByLabelText('One-time password')).toHaveAttribute('maxlength', '8');

    await user.type(screen.getByLabelText('One-time password'), '123456');
    await user.click(screen.getByRole('button', { name: 'Validate password' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Enter the 8 digit password.');
    expect(onValidate).not.toHaveBeenCalled();
  });

  it('submits the trimmed user id and the code', async () => {
    const user = userEvent.setup();
    const { onValidate } = renderForm({ defaultUserId: '' });

    await user.type(screen.getByLabelText('User Id'), '  alice  ');
    await user.type(screen.getByLabelText('One-time password'), '482913');
    await user.click(screen.getByRole('button', { name: 'Validate password' }));

    expect(onValidate).toHaveBeenCalledTimes(1);
    expect(onValidate).toHaveBeenCalledWith({ userId: 'alice', code: '482913' });
  });

  it('accepts a code with exactly the expected number of digits', async () => {
    const user = userEvent.setup();
    const { onValidate } = renderForm();

    await user.type(screen.getByLabelText('One-time password'), '482913');
    await user.click(screen.getByRole('button', { name: 'Validate password' }));

    expect(onValidate).toHaveBeenCalledWith({ userId: 'john.doe', code: '482913' });
  });

  it('requires a user id', async () => {
    const user = userEvent.setup();
    const { onValidate } = renderForm({ defaultUserId: '' });
    await user.type(screen.getByLabelText('One-time password'), '482913');

    await user.click(screen.getByRole('button', { name: 'Validate password' }));

    expect(screen.getByRole('alert')).toHaveTextContent('User Id is required.');
    expect(onValidate).not.toHaveBeenCalled();
  });

  it.each(['', '123', '12345'])(
    'rejects a code shorter than 6 digits (input "%s")',
    async (code) => {
      const user = userEvent.setup();
      const { onValidate } = renderForm();
      if (code !== '') {
        await user.type(screen.getByLabelText('One-time password'), code);
      }

      await user.click(screen.getByRole('button', { name: 'Validate password' }));

      expect(screen.getByRole('alert')).toHaveTextContent('Enter the 6 digit password.');
      expect(onValidate).not.toHaveBeenCalled();
    },
  );

  it('removes the validation message as soon as the user edits the form', async () => {
    const user = userEvent.setup();
    renderForm();
    await user.click(screen.getByRole('button', { name: 'Validate password' }));
    expect(screen.getByRole('alert')).toBeInTheDocument();

    await user.type(screen.getByLabelText('One-time password'), '1');

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a valid result as a success message', () => {
    renderForm({ result: createValidateResponse() });

    const result = screen.getByTestId('validation-result');
    expect(result).toHaveTextContent('Valid. The one-time password is valid.');
    expect(result).toHaveClass('message--success');
  });

  it('shows a rejected result as an error message with the explanation', () => {
    renderForm({
      result: createValidateResponse({
        isValid: false,
        status: 'NotFound',
        message: 'No active one-time password was found for this user. It may already have been used.',
      }),
    });

    const result = screen.getByTestId('validation-result');
    expect(result).toHaveTextContent(
      'Not valid. No active one-time password was found for this user. It may already have been used.',
    );
    expect(result).toHaveClass('message--error');
  });

  it('shows a transport error instead of a stale result', () => {
    renderForm({
      result: createValidateResponse(),
      error: 'Unable to reach the OTP service.',
    });

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to reach the OTP service.');
    expect(screen.queryByTestId('validation-result')).not.toBeInTheDocument();
  });

  it('disables the submit button and says so while validating', () => {
    renderForm({ isValidating: true });

    expect(screen.getByRole('button', { name: 'Validating…' })).toBeDisabled();
  });
});
